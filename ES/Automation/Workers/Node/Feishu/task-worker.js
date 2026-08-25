'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const lark = require('@larksuiteoapi/node-sdk');

const utf8 = 'utf8';
const requestTimeoutMs = 15000;
const maxRetries = 2;
const maximumNetworkAttempts = 20;
const networkAttempts = [];
const sensitiveValues = new Set();
const allowedTaskIds = new Set([
  'es.feishu.task.monitor',
  'es.feishu.task.dispatch',
  'es.feishu.task.transition',
  'es.feishu.message.send',
]);
const schemaHashes = {
  'es.feishu.task.monitor': 'e76103d13e908f0e9466c77cfb74c10f76fe9eaed0211e8443a62a37ef293eef',
  'es.feishu.task.dispatch': 'f8f33f8419f634b84ab6b0fd82e68fadc1bff0ba7c765502c4e907683b07480f',
  'es.feishu.task.transition': 'da3ec68c42da076cd9c11cbce01a2d5eca6eda00b9275587822867c1cf88705f',
  'es.feishu.message.send': '8e0d477c0f26236482bce5e6e2ec3ca5ac6b1f422e16dd58e6b28f58ac1089ee',
};

function sha256Buffer(buffer) {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

function sha256File(filePath) {
  return sha256Buffer(fs.readFileSync(filePath));
}

function stableToken(value) {
  return sha256Buffer(Buffer.from(String(value), utf8)).slice(0, 32);
}

function writeJsonAtomic(filePath, value) {
  const directory = path.dirname(filePath);
  fs.mkdirSync(directory, { recursive: true });
  const temporary = `${filePath}.${process.pid}.tmp`;
  fs.writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, { encoding: utf8, flag: 'wx' });
  fs.renameSync(temporary, filePath);
}

function requireString(value, field, maximum = 4096) {
  if (typeof value !== 'string' || value.trim().length === 0 || value.trim().length > maximum) {
    throw contractError('INVALID_INPUT', `${field} 必须是 1 到 ${maximum} 个字符。`, false);
  }
  return value.trim();
}

function optionalString(value, field, maximum = 4096) {
  if (value === undefined || value === null || value === '') return '';
  if (typeof value !== 'string' || value.length > maximum) {
    throw contractError('INVALID_INPUT', `${field} 最多 ${maximum} 个字符。`, false);
  }
  return value;
}

function contractError(code, message, retryable, details) {
  const error = new Error(message);
  error.contractCode = code;
  error.retryable = Boolean(retryable);
  if (details) error.details = details;
  return error;
}

function redact(message) {
  let text = String(message || '');
  for (const name of ['ES_FEISHU_APP_ID', 'ES_FEISHU_APP_SECRET']) {
    const value = process.env[name];
    if (value) text = text.split(value).join('[REDACTED]');
  }
  for (const value of sensitiveValues) {
    if (value) text = text.split(value).join('[REDACTED-ID]');
  }
  return text
    .replace(/(authorization|cookie|token|secret)\s*[:=]\s*[^\s,;]+/ig, '$1=[REDACTED]')
    .slice(0, 1000);
}

function normalizeRemoteError(error) {
  if (error && error.contractCode) return error;
  const status = Number(error && (error.statusCode || error.response?.status));
  const code = Number(error && (error.code || error.response?.data?.code));
  const message = redact(error && (error.msg || error.message || error.response?.data?.msg));
  const retryAfter = Number(error?.response?.headers?.['retry-after']);
  const retryAfterMs = Number.isFinite(retryAfter) && retryAfter >= 0
    ? Math.min(5000, Math.round(retryAfter * 1000)) : 0;
  if (status === 429 || code === 99991400) return contractError('RATE_LIMITED', message || 'Feishu rate limited.', true, { retryAfterMs });
  if (status === 401 || status === 403) return contractError(status === 401 ? 'AUTH_FAILED' : 'PERMISSION_DENIED', message, false);
  if (/permission|forbidden|no.?permission|权限|无权/i.test(message)) return contractError('PERMISSION_DENIED', message, false);
  if (/unauthorized|authentication|tenant_access_token|鉴权|认证/i.test(message)) return contractError('AUTH_FAILED', message, false);
  if (status === 404) return contractError('NOT_FOUND', message, false);
  if (status >= 500) return contractError('NETWORK_UNAVAILABLE', message, true);
  if (/timeout/i.test(message)) return contractError('REMOTE_TIMEOUT', message, true);
  if (/network|socket|ECONN|ENOTFOUND|reset/i.test(message)) return contractError('NETWORK_UNAVAILABLE', message, true);
  return contractError('REMOTE_REJECTED', message || 'Feishu API rejected the request.', false);
}

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function callRemote(label, action, retryBudget = maxRetries, uncertainOnTransient = false) {
  let last;
  for (let attempt = 0; attempt <= retryBudget; attempt += 1) {
    if (networkAttempts.length >= maximumNetworkAttempts) {
      throw contractError('REQUEST_BUDGET_EXCEEDED', 'Feishu request budget exhausted.', false);
    }
    try {
      const response = await action();
      if (!response || (response.code !== undefined && response.code !== 0)) {
        const remoteError = new Error(`${label}: ${response?.code ?? 'Unknown'} ${response?.msg ?? 'no details'}`);
        remoteError.code = response?.code;
        throw remoteError;
      }
      networkAttempts.push({ operation: label, attempt: attempt + 1, outcome: 'response', atUtc: new Date().toISOString() });
      return response;
    } catch (error) {
      last = normalizeRemoteError(error);
      networkAttempts.push({ operation: label, attempt: attempt + 1, outcome: last.contractCode, retryable: last.retryable, atUtc: new Date().toISOString() });
      if (!last.retryable || attempt === retryBudget) {
        if (last.retryable && uncertainOnTransient) {
          throw contractError('UNCERTAIN_REMOTE_RESULT', 'A write lost its terminal response; stop and reconcile before any new invocation.', false, {
            causeCode: last.contractCode, operation: label, mutationState: 'uncertain',
          });
        }
        throw last;
      }
      const backoff = last.details?.retryAfterMs || Math.min(2000, 250 * (2 ** attempt));
      await sleep(backoff);
    }
  }
  throw last;
}

function ensureApiSuccess(response, operation) {
  if (!response || (response.code !== undefined && response.code !== 0)) {
    const error = new Error(`${operation}: ${response?.code ?? 'Unknown'} ${response?.msg ?? 'no details'}`);
    error.code = response?.code;
    throw error;
  }
  return response.data || {};
}

function createClient() {
  const appId = process.env.ES_FEISHU_APP_ID;
  const appSecret = process.env.ES_FEISHU_APP_SECRET;
  if (!appId || !appSecret) {
    throw contractError('CREDENTIAL_MISSING', 'Managed Feishu credentials are unavailable.', false);
  }
  requireString(appId, 'ES_FEISHU_APP_ID', 256);
  requireString(appSecret, 'ES_FEISHU_APP_SECRET', 512);
  lark.defaultHttpInstance.defaults.timeout = requestTimeoutMs;
  return new lark.Client({
    appId,
    appSecret,
    appType: lark.AppType.SelfBuild,
    domain: lark.Domain.Feishu,
    loggerLevel: lark.LoggerLevel.error,
    httpInstance: lark.defaultHttpInstance,
    source: 'esframework-managed-worker',
  });
}

function normalizePageSize(value) {
  if (value === undefined || value === null) return 20;
  if (!Number.isInteger(value) || value < 1 || value > 50) {
    throw contractError('INVALID_INPUT', 'pageSize 必须是 1 到 50 的整数。', false);
  }
  return value;
}

function normalizeMembers(value) {
  if (value === undefined || value === null) return [];
  if (!Array.isArray(value) || value.length > 20) {
    throw contractError('INVALID_INPUT', 'members 必须是最多 20 项的数组。', false);
  }
  return value.map((item, index) => {
    if (!item || typeof item !== 'object') throw contractError('INVALID_INPUT', `members[${index}] 无效。`, false);
    const type = optionalString(item.type, `members[${index}].type`, 16) || 'user';
    const role = optionalString(item.role, `members[${index}].role`, 16) || 'assignee';
    if (!['user', 'app', 'chat'].includes(type) || !['assignee', 'follower', 'editor', 'viewer'].includes(role)) {
      throw contractError('INVALID_INPUT', `members[${index}] type/role 不在白名单。`, false);
    }
    const id = requireString(item.id, `members[${index}].id`, 128);
    sensitiveValues.add(id);
    return { id, type, role };
  });
}

function requireMemberRoles(value, allowedRoles, required = false) {
  const members = normalizeMembers(value);
  if (required && members.length === 0) {
    throw contractError('INVALID_INPUT', 'members 必须至少包含一项。', false);
  }
  if (members.some((member) => !allowedRoles.includes(member.role))) {
    throw contractError('INVALID_INPUT', 'member.role 不属于当前 operation 的允许角色。', false);
  }
  return members;
}

function validateClaimedRoles(value, allowedRoles) {
  if (value === undefined || value === null) return;
  if (!Array.isArray(value) || value.length < 1 || value.length > 20) {
    throw contractError('INVALID_INPUT', 'claimedRoles 必须是 1 到 20 项的数组。', false);
  }
  const seen = new Set();
  for (const [index, binding] of value.entries()) {
    if (!binding || typeof binding !== 'object' || Array.isArray(binding)) {
      throw contractError('INVALID_INPUT', `claimedRoles[${index}] 无效。`, false);
    }
    rejectUnknownKeys(binding, ['roleId', 'role'], `claimedRoles[${index}]`);
    const roleId = requireString(binding.roleId, `claimedRoles[${index}].roleId`, 48);
    if (!/^[a-z0-9][a-z0-9._-]{1,47}$/.test(roleId) || seen.has(roleId)
        || !allowedRoles.includes(binding.role)) {
      throw contractError('INVALID_INPUT', `claimedRoles[${index}] 的 roleId/role 无效或重复。`, false);
    }
    seen.add(roleId);
  }
}

function rejectUnknownKeys(value, allowedKeys, field = 'input') {
  const unknown = Object.keys(value).find((key) => !allowedKeys.includes(key));
  if (unknown) throw contractError('INVALID_INPUT', `${field} 包含未注册字段：${unknown}`, false);
}

function normalizeTask(task) {
  if (!task || typeof task !== 'object') return null;
  return {
    guid: task.guid || '',
    summary: optionalString(task.summary, 'remote.summary', 200),
    description: optionalString(task.description, 'remote.description', 4000),
    start: task.start || null,
    due: task.due || null,
    completedAt: task.completed_at || '',
    createdAt: task.created_at || '',
    updatedAt: task.updated_at || '',
    status: task.status || '',
    url: task.url || '',
    tasklists: Array.isArray(task.tasklists) ? task.tasklists.slice(0, 20) : [],
    members: Array.isArray(task.members) ? task.members.slice(0, 20).map((member) => ({
      idHash: stableToken(member.id || ''),
      type: member.type || '',
      role: member.role || '',
      name: optionalString(member.name, 'remote.member.name', 100),
    })) : [],
    agentTaskStatus: Number.isInteger(task.agent_task_status) ? task.agent_task_status : null,
    agentTaskProgress: optionalString(task.agent_task_progress, 'remote.agentTaskProgress', 2000),
    reminders: Array.isArray(task.reminders) ? task.reminders.slice(0, 10) : [],
  };
}

function normalizeTasklist(tasklist) {
  if (!tasklist || typeof tasklist !== 'object') return null;
  return {
    guid: tasklist.guid || '',
    name: optionalString(tasklist.name, 'remote.tasklist.name', 100),
    url: tasklist.url || '',
    createdAt: tasklist.created_at || '',
    updatedAt: tasklist.updated_at || '',
    archivedAt: tasklist.archive_msec || '',
    members: Array.isArray(tasklist.members) ? tasklist.members.slice(0, 20).map((member) => ({
      idHash: stableToken(member.id || ''), type: member.type || '', role: member.role || '',
    })) : [],
  };
}

async function getTask(client, taskGuid) {
  const response = await callRemote('task-get', () => client.task.v2.task.get({ path: { task_guid: taskGuid } }));
  return ensureApiSuccess(response, 'task-get').task;
}

async function verifyExpectedVersion(client, input) {
  const current = await getTask(client, requireString(input.taskGuid, 'taskGuid', 128));
  const expected = requireString(input.expectedUpdatedAt, 'expectedUpdatedAt', 64);
  if (!current || String(current.updated_at || '') !== expected) {
    throw contractError('REMOTE_VERSION_CONFLICT', '任务 updated_at 已变化；拒绝覆盖人工或并发更新。', false, {
      expectedUpdatedAt: expected,
      actualUpdatedAt: String(current?.updated_at || ''),
    });
  }
  return current;
}

async function executeMonitor(client, input) {
  const pageSize = normalizePageSize(input.pageSize);
  const pageToken = optionalString(input.pageToken, 'pageToken', 512);
  switch (input.operation) {
    case 'tasklist-list': {
      const response = await callRemote('tasklist-list', () => client.task.v2.tasklist.list({
        params: { page_size: pageSize, ...(pageToken ? { page_token: pageToken } : {}) },
      }));
      const data = ensureApiSuccess(response, input.operation);
      return { items: (data.items || []).slice(0, pageSize).map(normalizeTasklist), hasMore: Boolean(data.has_more), pageToken: data.page_token || '' };
    }
    case 'tasklist-get': {
      const tasklistGuid = requireString(input.tasklistGuid, 'tasklistGuid', 128);
      const response = await callRemote('tasklist-get', () => client.task.v2.tasklist.get({ path: { tasklist_guid: tasklistGuid } }));
      return { tasklist: normalizeTasklist(ensureApiSuccess(response, input.operation).tasklist) };
    }
    case 'task-list': {
      const tasklistGuid = requireString(input.tasklistGuid, 'tasklistGuid', 128);
      const response = await callRemote('task-list', () => client.task.v2.tasklist.tasks({
        path: { tasklist_guid: tasklistGuid },
        params: { page_size: pageSize, ...(pageToken ? { page_token: pageToken } : {}), ...(typeof input.completed === 'boolean' ? { completed: input.completed } : {}) },
      }));
      const data = ensureApiSuccess(response, input.operation);
      let items = (data.items || []).slice(0, pageSize).map(normalizeTask);
      if (input.includeDetails) {
        if (pageSize > 10) throw contractError('INVALID_INPUT', 'includeDetails 要求 pageSize 不超过 10。', false);
        const detailed = [];
        for (const item of items) detailed.push(normalizeTask(await getTask(client, requireString(item.guid, 'task.guid', 128))));
        items = detailed;
      }
      return { items, detailed: Boolean(input.includeDetails), hasMore: Boolean(data.has_more), pageToken: data.page_token || '' };
    }
    case 'task-get':
      return { task: normalizeTask(await getTask(client, requireString(input.taskGuid, 'taskGuid', 128))) };
    default:
      throw contractError('INVALID_INPUT', `monitor 不支持操作：${input.operation}`, false);
  }
}

function buildTaskData(input, clientToken) {
  const data = {
    summary: requireString(input.summary, 'summary', 200),
    description: optionalString(input.description, 'description', 4000),
    client_token: clientToken,
  };
  if (input.tasklistGuid) data.tasklists = [{ tasklist_guid: requireString(input.tasklistGuid, 'tasklistGuid', 128) }];
  const members = requireMemberRoles(input.members, ['assignee', 'follower']);
  if (members.length) data.members = members;
  if (input.startTimestamp) data.start = { timestamp: requireString(input.startTimestamp, 'startTimestamp', 16), is_all_day: Boolean(input.isAllDay) };
  if (input.dueTimestamp) data.due = { timestamp: requireString(input.dueTimestamp, 'dueTimestamp', 16), is_all_day: Boolean(input.isAllDay) };
  return data;
}

async function findTasklistByExactName(client, name) {
  const response = await callRemote('tasklist-search', () => client.task.v2.tasklist.search({ data: { query: name }, params: { page_size: 50 } }));
  const data = ensureApiSuccess(response, 'tasklist-search');
  const match = (data.items || []).find((item) => item && item.display_info === name);
  return match ? { guid: match.id, name: match.display_info, url: match.meta_data?.app_link || '' } : null;
}

async function createOrFindTasklist(client, name, members) {
  const existing = await findTasklistByExactName(client, name);
  if (existing) return { tasklist: existing, recovered: true };
  try {
    const response = await callRemote('tasklist-create', () => client.task.v2.tasklist.create({ data: { name, ...(members.length ? { members } : {}) } }), 0);
    return { tasklist: normalizeTasklist(ensureApiSuccess(response, 'tasklist-create').tasklist), recovered: false };
  } catch (error) {
    const normalized = normalizeRemoteError(error);
    if (!normalized.retryable) throw normalized;
    try {
      const recovered = await findTasklistByExactName(client, name);
      if (recovered) return { tasklist: recovered, recovered: true };
    } catch (_) {
      // The recovery search is evidence collection only; the original write remains uncertain.
    }
    throw contractError('UNCERTAIN_REMOTE_RESULT', 'Task-list creation response was lost and exact-name recovery found no object.', false, {
      targetNameHash: stableToken(name), mutationState: 'uncertain',
    });
  }
}

function fixtureTasks(prefix) {
  return [
    ['Product Owner', '定义范围、优先级和验收条件'],
    ['Technical Lead', '完成架构评审、依赖与风险登记'],
    ['Developer', '实现最小可交付切片并绑定源码证据'],
    ['QA', '执行正向、失败、取消和恢复验收'],
    ['Release Owner', '核对发布证据、回滚点和上线范围'],
  ].map(([role, description], index) => ({ summary: `${prefix} ${index + 1}/5 ${role}`, description }));
}

async function executeDispatch(client, input, runId) {
  const suffix = stableToken(runId).slice(0, 12);
  switch (input.operation) {
    case 'tasklist-create': {
      const requested = requireString(input.tasklistName, 'tasklistName', 80);
      const finalName = `${requested} [ES:${suffix}]`;
      return createOrFindTasklist(client, finalName, requireMemberRoles(input.members, ['editor', 'viewer']));
    }
    case 'task-create': {
      const data = buildTaskData(input, stableToken(`${runId}:task:0`));
      const response = await callRemote('task-create', () => client.task.v2.task.create({ data }));
      return { task: normalizeTask(ensureApiSuccess(response, input.operation).task) };
    }
    case 'virtual-team-fixture-create': {
      const prefix = optionalString(input.fixturePrefix, 'fixturePrefix', 40) || 'ES Virtual Team';
      const finalName = `${prefix} [ES-TEST:${suffix}]`;
      const listResult = await createOrFindTasklist(client, finalName, []);
      const tasklistGuid = requireString(listResult.tasklist.guid, 'created.tasklist.guid', 128);
      const requestedTasks = Array.isArray(input.tasks) && input.tasks.length ? input.tasks : fixtureTasks(prefix);
      if (requestedTasks.length > 20) throw contractError('INVALID_INPUT', '测试夹具最多 20 个任务。', false);
      const tasks = [];
      for (let index = 0; index < requestedTasks.length; index += 1) {
        const source = requestedTasks[index];
        const data = buildTaskData({ ...source, tasklistGuid }, stableToken(`${runId}:fixture:${index}`));
        try {
          const response = await callRemote(`fixture-task-${index}`, () => client.task.v2.task.create({ data }));
          tasks.push(normalizeTask(ensureApiSuccess(response, `fixture-task-${index}`).task));
        } catch (error) {
          const normalized = normalizeRemoteError(error);
          throw contractError('PARTIAL_SUCCESS', 'Virtual-team fixture stopped after a task creation failure.', false, {
            causeCode: normalized.contractCode,
            tasklist: listResult.tasklist,
            tasklistRecovered: listResult.recovered,
            createdTasks: tasks,
            failedTaskIndex: index,
            mutationState: 'partial',
          });
        }
      }
      return { tasklist: listResult.tasklist, tasklistRecovered: listResult.recovered, tasks };
    }
    default:
      throw contractError('INVALID_INPUT', `dispatch 不支持操作：${input.operation}`, false);
  }
}

function buildPatch(input) {
  const task = {};
  const updateFields = [];
  const set = (field, value) => { task[field] = value; updateFields.push(field); };
  if (input.summary !== undefined) set('summary', requireString(input.summary, 'summary', 200));
  if (input.description !== undefined) set('description', optionalString(input.description, 'description', 4000));
  if (input.startTimestamp !== undefined) set('start', { timestamp: requireString(input.startTimestamp, 'startTimestamp', 16), is_all_day: Boolean(input.isAllDay) });
  if (input.dueTimestamp !== undefined) set('due', { timestamp: requireString(input.dueTimestamp, 'dueTimestamp', 16), is_all_day: Boolean(input.isAllDay) });
  if (input.agentTaskStatus !== undefined) {
    if (!Number.isInteger(input.agentTaskStatus) || input.agentTaskStatus < 0 || input.agentTaskStatus > 100) throw contractError('INVALID_INPUT', 'agentTaskStatus 必须是 0 到 100 的整数。', false);
    set('agent_task_status', input.agentTaskStatus);
  }
  if (input.agentTaskProgress !== undefined) set('agent_task_progress', optionalString(input.agentTaskProgress, 'agentTaskProgress', 2000));
  if (!updateFields.length) throw contractError('INVALID_INPUT', 'task-update 没有允许的更新字段。', false);
  return { task, update_fields: updateFields };
}

async function executeTransition(client, input) {
  const taskGuid = requireString(input.taskGuid, 'taskGuid', 128);
  const current = await verifyExpectedVersion(client, input);
  let response;
  switch (input.operation) {
    case 'task-update':
      response = await callRemote('task-update', () => client.task.v2.task.patch({ path: { task_guid: taskGuid }, data: buildPatch(input) }), 0, true);
      break;
    case 'task-complete':
      response = await callRemote('task-complete', () => client.task.v2.task.patch({ path: { task_guid: taskGuid }, data: { task: { completed_at: String(Date.now()) }, update_fields: ['completed_at'] } }), 0, true);
      break;
    case 'task-reopen':
      response = await callRemote('task-reopen', () => client.task.v2.task.patch({ path: { task_guid: taskGuid }, data: { task: { completed_at: '0' }, update_fields: ['completed_at'] } }), 0, true);
      break;
    case 'members-add':
      response = await callRemote('members-add', () => client.task.v2.task.addMembers({ path: { task_guid: taskGuid }, data: { members: requireMemberRoles(input.members, ['assignee', 'follower'], true) } }), 0, true);
      break;
    case 'members-remove':
      response = await callRemote('members-remove', () => client.task.v2.task.removeMembers({ path: { task_guid: taskGuid }, data: { members: requireMemberRoles(input.members, ['assignee', 'follower'], true).map(({ id, type }) => ({ id, type })) } }), 0, true);
      break;
    case 'reminder-add': {
      const minute = input.relativeFireMinute;
      if (!Number.isInteger(minute) || minute < -525600 || minute > 0) throw contractError('INVALID_INPUT', 'relativeFireMinute 必须位于 -525600 到 0。', false);
      response = await callRemote('reminder-add', () => client.task.v2.task.addReminders({ path: { task_guid: taskGuid }, data: { reminders: [{ relative_fire_minute: minute }] } }), 0, true);
      break;
    }
    case 'reminder-remove':
      response = await callRemote('reminder-remove', () => client.task.v2.task.removeReminders({ path: { task_guid: taskGuid }, data: { reminder_ids: [requireString(input.reminderId, 'reminderId', 128)] } }), 0, true);
      break;
    default:
      throw contractError('INVALID_INPUT', `transition 不支持操作：${input.operation}`, false);
  }
  const data = ensureApiSuccess(response, input.operation);
  return { previousUpdatedAt: current.updated_at || '', task: normalizeTask(data.task) };
}

async function executeMessage(client, input, runId) {
  const roleId = requireString(input.roleId, 'roleId', 48);
  if (!/^[a-z0-9][a-z0-9._-]{1,47}$/.test(roleId)) {
    throw contractError('INVALID_INPUT', 'roleId 格式无效。', false);
  }
  const recipientId = requireString(input.recipientId, 'recipientId', 128);
  const recipientType = requireString(input.recipientType, 'recipientType', 16);
  if (!['open_id', 'user_id', 'union_id', 'email', 'chat_id'].includes(recipientType)) {
    throw contractError('INVALID_INPUT', 'recipientType 不在单收件人白名单。', false);
  }
  const recipientRefHash = requireString(input.recipientRefHash, 'recipientRefHash', 64);
  if (!/^[a-f0-9]{64}$/.test(recipientRefHash)) {
    throw contractError('INVALID_INPUT', 'recipientRefHash 无效。', false);
  }
  const text = requireString(input.text, 'text', 1000);
  sensitiveValues.add(recipientId);
  sensitiveValues.add(text);
  const uuid = stableToken(`${runId}:message:0`);
  const response = await callRemote('message-send-text', () => client.im.v1.message.create({
    data: {
      receive_id: recipientId,
      msg_type: 'text',
      content: JSON.stringify({ text }),
      uuid,
    },
    params: { receive_id_type: recipientType },
  }), maxRetries, true);
  const data = ensureApiSuccess(response, 'message-send-text');
  return {
    recipientRefHash,
    idempotencyUuidHash: stableToken(uuid),
    message: {
      messageId: data.message_id || '',
      createTime: data.create_time || '',
      updateTime: data.update_time || '',
      msgType: data.msg_type || 'text',
    },
  };
}

function dryRunPlan(input) {
  const mutation = input.taskId !== 'es.feishu.task.monitor';
  const plan = {
    operation: input.operation,
    taskId: input.taskId,
    mutation,
    networkCalled: false,
    mutationApplied: false,
    bounds: { maxBatchTasks: 20, maxMembers: 20, requestTimeoutMs, maxRetries },
  };
  if (input.operation === 'virtual-team-fixture-create') {
    plan.fixture = { tasklistNamePrefix: optionalString(input.input?.fixturePrefix, 'fixturePrefix', 40) || 'ES Virtual Team', roles: fixtureTasks('ES').map((item) => item.summary) };
  }
  if (input.operation === 'send-text') {
    plan.message = {
      recipientRefHash: requireString(input.input?.recipientRefHash, 'recipientRefHash', 64),
      contentHash: stableToken(requireString(input.input?.text, 'text', 1000)),
      recipientCount: 1,
      msgType: 'text',
      serverIdempotency: 'uuid',
    };
  }
  return plan;
}

function validateOperationInput(taskId, input, runId, dryRun) {
  if (taskId !== 'es.feishu.task.monitor' && dryRun && input.dryRunEvidenceRunId !== undefined) {
    throw contractError('INVALID_INPUT', 'DryRun must not reference another DryRun receipt.', false);
  }
  if (taskId !== 'es.feishu.task.monitor' && !dryRun
      && (typeof input.dryRunEvidenceRunId !== 'string'
        || !/^[0-9a-f]{32}$/.test(input.dryRunEvidenceRunId))) {
    throw contractError('INVALID_INPUT', 'Live mutation requires dryRunEvidenceRunId.', false);
  }
  if (taskId === 'es.feishu.task.monitor') {
    const allowed = input.operation === 'tasklist-list' ? ['operation', 'pageSize', 'pageToken']
      : input.operation === 'tasklist-get' ? ['operation', 'tasklistGuid']
        : input.operation === 'task-list' ? ['operation', 'tasklistGuid', 'pageSize', 'pageToken', 'completed', 'includeDetails']
          : ['operation', 'taskGuid'];
    rejectUnknownKeys(input, allowed);
    normalizePageSize(input.pageSize);
    optionalString(input.pageToken, 'pageToken', 512);
    if (input.operation === 'tasklist-get' || input.operation === 'task-list') {
      requireString(input.tasklistGuid, 'tasklistGuid', 128);
    } else if (input.operation === 'task-get') {
      requireString(input.taskGuid, 'taskGuid', 128);
    } else if (input.operation !== 'tasklist-list') {
      throw contractError('INVALID_INPUT', `monitor 不支持操作：${input.operation}`, false);
    }
    if (input.completed !== undefined && typeof input.completed !== 'boolean') {
      throw contractError('INVALID_INPUT', 'completed 必须是布尔值。', false);
    }
    if (input.includeDetails !== undefined && typeof input.includeDetails !== 'boolean') {
      throw contractError('INVALID_INPUT', 'includeDetails 必须是布尔值。', false);
    }
    if (input.includeDetails && (input.operation !== 'task-list' || normalizePageSize(input.pageSize) > 10)) {
      throw contractError('INVALID_INPUT', 'includeDetails 仅用于 task-list，且 pageSize 不得超过 10。', false);
    }
    return;
  }
  if (taskId === 'es.feishu.task.dispatch') {
    if (input.operation === 'tasklist-create') {
      rejectUnknownKeys(input, ['operation', 'dryRunEvidenceRunId', 'tasklistName', 'members', 'claimedRoles', 'claimedRoleResolutionHash']);
      requireString(input.tasklistName, 'tasklistName', 80);
      requireMemberRoles(input.members, ['editor', 'viewer']);
      validateClaimedRoles(input.claimedRoles, ['editor', 'viewer']);
      if (input.claimedRoles && !/^[a-f0-9]{64}$/.test(input.claimedRoleResolutionHash || '')) {
        throw contractError('INVALID_INPUT', 'claimedRoleResolutionHash 无效。', false);
      }
    } else if (input.operation === 'task-create') {
      rejectUnknownKeys(input, ['operation', 'dryRunEvidenceRunId', 'tasklistGuid', 'summary', 'description', 'startTimestamp', 'dueTimestamp', 'isAllDay', 'members', 'claimedRoles', 'claimedRoleResolutionHash']);
      requireString(input.tasklistGuid, 'tasklistGuid', 128);
      buildTaskData(input, stableToken(`${runId}:validation`));
      validateClaimedRoles(input.claimedRoles, ['assignee', 'follower']);
      if (input.claimedRoles && !/^[a-f0-9]{64}$/.test(input.claimedRoleResolutionHash || '')) {
        throw contractError('INVALID_INPUT', 'claimedRoleResolutionHash 无效。', false);
      }
    } else if (input.operation === 'virtual-team-fixture-create') {
      rejectUnknownKeys(input, ['operation', 'dryRunEvidenceRunId', 'fixturePrefix', 'tasks']);
      optionalString(input.fixturePrefix, 'fixturePrefix', 40);
      if (input.tasks !== undefined) {
        if (!Array.isArray(input.tasks) || input.tasks.length < 1 || input.tasks.length > 20) {
          throw contractError('INVALID_INPUT', 'tasks 必须是 1 到 20 项的数组。', false);
        }
        for (const [index, task] of input.tasks.entries()) {
          if (!task || typeof task !== 'object' || Array.isArray(task)) {
            throw contractError('INVALID_INPUT', `tasks[${index}] 无效。`, false);
          }
          const keys = Object.keys(task);
          if (keys.some((key) => !['summary', 'description'].includes(key))) {
            throw contractError('INVALID_INPUT', `tasks[${index}] 包含未注册字段。`, false);
          }
          requireString(task.summary, `tasks[${index}].summary`, 200);
          optionalString(task.description, `tasks[${index}].description`, 4000);
        }
      }
    } else {
      throw contractError('INVALID_INPUT', `dispatch 不支持操作：${input.operation}`, false);
    }
    return;
  }

  if (taskId === 'es.feishu.message.send') {
    rejectUnknownKeys(input, ['operation', 'dryRunEvidenceRunId', 'roleId', 'text',
      'recipientId', 'recipientType', 'recipientRefHash']);
    if (input.operation !== 'send-text') {
      throw contractError('INVALID_INPUT', `message 不支持操作：${input.operation}`, false);
    }
    const roleId = requireString(input.roleId, 'roleId', 48);
    if (!/^[a-z0-9][a-z0-9._-]{1,47}$/.test(roleId)) {
      throw contractError('INVALID_INPUT', 'roleId 格式无效。', false);
    }
    requireString(input.text, 'text', 1000);
    requireString(input.recipientId, 'recipientId', 128);
    if (!['open_id', 'user_id', 'union_id', 'email', 'chat_id'].includes(input.recipientType)) {
      throw contractError('INVALID_INPUT', 'recipientType 无效。', false);
    }
    if (!/^[a-f0-9]{64}$/.test(input.recipientRefHash || '')) {
      throw contractError('INVALID_INPUT', 'recipientRefHash 无效。', false);
    }
    return;
  }

  requireString(input.taskGuid, 'taskGuid', 128);
  requireString(input.expectedUpdatedAt, 'expectedUpdatedAt', 64);
  const transitionBase = ['operation', 'taskGuid', 'expectedUpdatedAt', 'dryRunEvidenceRunId'];
  if (input.operation === 'task-update') {
    rejectUnknownKeys(input, transitionBase.concat(['summary', 'description', 'startTimestamp', 'dueTimestamp', 'isAllDay', 'agentTaskStatus', 'agentTaskProgress']));
    buildPatch(input);
  } else if (input.operation === 'task-complete' || input.operation === 'task-reopen') {
    rejectUnknownKeys(input, transitionBase);
  }
  else if (input.operation === 'members-add' || input.operation === 'members-remove') {
    rejectUnknownKeys(input, transitionBase.concat(['members', 'claimedRoles', 'claimedRoleResolutionHash']));
    requireMemberRoles(input.members, ['assignee', 'follower'], true);
    validateClaimedRoles(input.claimedRoles, ['assignee', 'follower']);
    if (input.claimedRoles && !/^[a-f0-9]{64}$/.test(input.claimedRoleResolutionHash || '')) {
      throw contractError('INVALID_INPUT', 'claimedRoleResolutionHash 无效。', false);
    }
  } else if (input.operation === 'reminder-add') {
    rejectUnknownKeys(input, transitionBase.concat(['relativeFireMinute']));
    if (!Number.isInteger(input.relativeFireMinute)
        || input.relativeFireMinute < -525600 || input.relativeFireMinute > 0) {
      throw contractError('INVALID_INPUT', 'relativeFireMinute 必须位于 -525600 到 0。', false);
    }
  } else if (input.operation === 'reminder-remove') {
    rejectUnknownKeys(input, transitionBase.concat(['reminderId']));
    requireString(input.reminderId, 'reminderId', 128);
  } else {
    throw contractError('INVALID_INPUT', `transition 不支持操作：${input.operation}`, false);
  }
}

async function execute(input) {
  if (!allowedTaskIds.has(input.taskId) || input.taskVersion !== 1) throw contractError('INVALID_INPUT', 'Task identity invalid.', false);
  if (input.entrypointHash !== sha256File(__filename)
      || input.inputSchemaHash !== schemaHashes[input.taskId]) {
    throw contractError('SOURCE_DRIFT', 'Worker or input Schema hash drifted.', false);
  }
  const operation = requireString(input.operation, 'operation', 64);
  const operationInput = input.input && typeof input.input === 'object' ? input.input : {};
  const normalized = { ...operationInput, operation };
  validateOperationInput(input.taskId, normalized, input.runId, Boolean(input.dryRun));
  if (input.dryRun) return { dryRun: true, networkCalled: false, mutationApplied: false, networkAttempts: [], plan: dryRunPlan({ ...input, input: normalized }) };
  const client = createClient();
  let data;
  if (input.taskId === 'es.feishu.task.monitor') data = await executeMonitor(client, normalized);
  else if (input.taskId === 'es.feishu.task.dispatch') data = await executeDispatch(client, normalized, input.runId);
  else if (input.taskId === 'es.feishu.message.send') data = await executeMessage(client, normalized, input.runId);
  else data = await executeTransition(client, normalized);
  return { dryRun: false, networkCalled: true, mutationApplied: input.taskId !== 'es.feishu.task.monitor', operation, networkAttempts: networkAttempts.slice(0, 20), data };
}

async function main() {
  const inputPath = path.resolve(requireString(process.argv[2], 'inputPath'));
  const outputDirectory = path.resolve(requireString(process.argv[3], 'outputDirectory'));
  const startedAtUtc = new Date().toISOString();
  const input = JSON.parse(fs.readFileSync(inputPath, utf8));
  const inputManifestHash = sha256File(inputPath);
  const resultPath = path.join(outputDirectory, 'result.json');
  const dataPath = path.join(outputDirectory, 'feishu-task-data.json');
  const baseResult = {
    protocolVersion: 1,
    taskId: input.taskId,
    taskVersion: input.taskVersion,
    runId: input.runId,
    workerType: input.workerType,
    workerId: input.workerId,
    workerVersion: input.workerVersion,
    entrypointHash: input.entrypointHash,
    status: 'Failed',
    exitCode: 1,
    startedAtUtc,
    finishedAtUtc: '',
    inputManifestHash,
    outputs: [],
    outputHashes: [],
    findings: [],
    errors: [],
  };

  try {
    const data = await execute(input);
    writeJsonAtomic(dataPath, data);
    baseResult.status = input.dryRun ? 'DryRun' : 'Passed';
    baseResult.exitCode = 0;
    baseResult.outputs.push(dataPath);
    baseResult.outputHashes.push(sha256File(dataPath));
    baseResult.findings.push(input.dryRun ? 'Feishu task DryRun completed without network access.' : `Feishu task operation completed: ${input.operation}`);
  } catch (rawError) {
    const error = normalizeRemoteError(rawError);
    baseResult.status = ['CREDENTIAL_MISSING', 'AUTH_FAILED', 'PERMISSION_DENIED', 'REMOTE_VERSION_CONFLICT'].includes(error.contractCode) ? 'Blocked' : 'Failed';
    const safeDetails = error.details || null;
    baseResult.errors.push(JSON.stringify({ code: error.contractCode, message: redact(error.message), retryable: Boolean(error.retryable), details: safeDetails }));
    if (!input.dryRun && networkAttempts.length > 0) {
      const mutationState = safeDetails?.mutationState
        || (input.taskId === 'es.feishu.task.monitor' ? 'none' : 'not-confirmed');
      const failureData = {
        dryRun: false,
        operation: input.operation,
        networkCalled: true,
        mutationApplied: mutationState === 'partial' ? true : null,
        mutationState,
        networkAttempts: networkAttempts.slice(0, 20),
        partialResult: mutationState === 'partial' ? safeDetails : null,
      };
      writeJsonAtomic(dataPath, failureData);
      baseResult.outputs.push(dataPath);
      baseResult.outputHashes.push(sha256File(dataPath));
    }
  }

  baseResult.finishedAtUtc = new Date().toISOString();
  writeJsonAtomic(resultPath, baseResult);
  process.exitCode = baseResult.exitCode;
}

main().catch((rawError) => {
  const error = normalizeRemoteError(rawError);
  process.stderr.write(`${JSON.stringify({ code: error.contractCode, message: redact(error.message) })}\n`);
  process.exitCode = 1;
});
