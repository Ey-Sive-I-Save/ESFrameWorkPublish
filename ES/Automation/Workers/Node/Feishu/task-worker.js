'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const lark = require('@larksuiteoapi/node-sdk');

const utf8 = 'utf8';
const requestTimeoutMs = 15000;
const maxRetries = 2;
const allowedTaskIds = new Set([
  'es.feishu.task.monitor',
  'es.feishu.task.dispatch',
  'es.feishu.task.transition',
]);
const schemaHashes = {
  'es.feishu.task.monitor': 'e9752b11191b19f155e36629e5fefcfb3ad8900a854f94057d3d980d57c28345',
  'es.feishu.task.dispatch': '90aafeb355a263fbcd10e91897d1f26a702767e9b0a97a5278d8114c7fc7dc8b',
  'es.feishu.task.transition': '95008f8bc2bbe84c34b4340c990fb19d0e010432bdd1625f1911e285cc2475d9',
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
  return text
    .replace(/(authorization|cookie|token|secret)\s*[:=]\s*[^\s,;]+/ig, '$1=[REDACTED]')
    .slice(0, 1000);
}

function normalizeRemoteError(error) {
  if (error && error.contractCode) return error;
  const status = Number(error && (error.statusCode || error.response?.status));
  const code = Number(error && (error.code || error.response?.data?.code));
  const message = redact(error && (error.msg || error.message || error.response?.data?.msg));
  if (status === 429 || code === 99991400) return contractError('RATE_LIMITED', message || 'Feishu rate limited.', true);
  if (status === 401 || status === 403) return contractError(status === 401 ? 'AUTH_FAILED' : 'PERMISSION_DENIED', message, false);
  if (status === 404) return contractError('NOT_FOUND', message, false);
  if (status >= 500) return contractError('NETWORK_UNAVAILABLE', message, true);
  if (/timeout/i.test(message)) return contractError('REMOTE_TIMEOUT', message, true);
  if (/network|socket|ECONN|ENOTFOUND|reset/i.test(message)) return contractError('NETWORK_UNAVAILABLE', message, true);
  return contractError('REMOTE_REJECTED', message || 'Feishu API rejected the request.', false);
}

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function callRemote(action) {
  let last;
  for (let attempt = 0; attempt <= maxRetries; attempt += 1) {
    try {
      return await action();
    } catch (error) {
      last = normalizeRemoteError(error);
      if (!last.retryable || attempt === maxRetries) throw last;
      await sleep(Math.min(2000, 250 * (2 ** attempt)));
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
  const appId = requireString(process.env.ES_FEISHU_APP_ID, 'ES_FEISHU_APP_ID', 256);
  const appSecret = requireString(process.env.ES_FEISHU_APP_SECRET, 'ES_FEISHU_APP_SECRET', 512);
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
    return { id: requireString(item.id, `members[${index}].id`, 128), type, role };
  });
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
  const response = await callRemote(() => client.task.v2.task.get({ path: { task_guid: taskGuid } }));
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
      const response = await callRemote(() => client.task.v2.tasklist.list({
        params: { page_size: pageSize, ...(pageToken ? { page_token: pageToken } : {}) },
      }));
      const data = ensureApiSuccess(response, input.operation);
      return { items: (data.items || []).slice(0, pageSize).map(normalizeTasklist), hasMore: Boolean(data.has_more), pageToken: data.page_token || '' };
    }
    case 'tasklist-get': {
      const tasklistGuid = requireString(input.tasklistGuid, 'tasklistGuid', 128);
      const response = await callRemote(() => client.task.v2.tasklist.get({ path: { tasklist_guid: tasklistGuid } }));
      return { tasklist: normalizeTasklist(ensureApiSuccess(response, input.operation).tasklist) };
    }
    case 'task-list': {
      const tasklistGuid = requireString(input.tasklistGuid, 'tasklistGuid', 128);
      const response = await callRemote(() => client.task.v2.tasklist.tasks({
        path: { tasklist_guid: tasklistGuid },
        params: { page_size: pageSize, ...(pageToken ? { page_token: pageToken } : {}), ...(typeof input.completed === 'boolean' ? { completed: input.completed } : {}) },
      }));
      const data = ensureApiSuccess(response, input.operation);
      return { items: (data.items || []).slice(0, pageSize).map(normalizeTask), hasMore: Boolean(data.has_more), pageToken: data.page_token || '' };
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
  const members = normalizeMembers(input.members);
  if (members.length) data.members = members.filter((member) => ['assignee', 'follower'].includes(member.role));
  if (input.startTimestamp) data.start = { timestamp: requireString(input.startTimestamp, 'startTimestamp', 16), is_all_day: Boolean(input.isAllDay) };
  if (input.dueTimestamp) data.due = { timestamp: requireString(input.dueTimestamp, 'dueTimestamp', 16), is_all_day: Boolean(input.isAllDay) };
  return data;
}

async function findTasklistByExactName(client, name) {
  const response = await callRemote(() => client.task.v2.tasklist.search({ data: { query: name }, params: { page_size: 50 } }));
  const data = ensureApiSuccess(response, 'tasklist-search');
  const match = (data.items || []).find((item) => item && item.display_info === name);
  return match ? { guid: match.id, name: match.display_info, url: match.meta_data?.app_link || '' } : null;
}

async function createOrFindTasklist(client, name, members) {
  const existing = await findTasklistByExactName(client, name);
  if (existing) return { tasklist: existing, recovered: true };
  const response = await callRemote(() => client.task.v2.tasklist.create({ data: { name, ...(members.length ? { members } : {}) } }));
  return { tasklist: normalizeTasklist(ensureApiSuccess(response, 'tasklist-create').tasklist), recovered: false };
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
      return createOrFindTasklist(client, finalName, normalizeMembers(input.members).filter((member) => ['editor', 'viewer'].includes(member.role)));
    }
    case 'task-create': {
      const data = buildTaskData(input, stableToken(`${runId}:task:0`));
      const response = await callRemote(() => client.task.v2.task.create({ data }));
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
        const response = await callRemote(() => client.task.v2.task.create({ data }));
        tasks.push(normalizeTask(ensureApiSuccess(response, `fixture-task-${index}`).task));
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
      response = await callRemote(() => client.task.v2.task.patch({ path: { task_guid: taskGuid }, data: buildPatch(input) }));
      break;
    case 'task-complete':
      response = await callRemote(() => client.task.v2.task.patch({ path: { task_guid: taskGuid }, data: { task: { completed_at: String(Date.now()) }, update_fields: ['completed_at'] } }));
      break;
    case 'task-reopen':
      response = await callRemote(() => client.task.v2.task.patch({ path: { task_guid: taskGuid }, data: { task: { completed_at: '0' }, update_fields: ['completed_at'] } }));
      break;
    case 'members-add':
      response = await callRemote(() => client.task.v2.task.addMembers({ path: { task_guid: taskGuid }, data: { members: normalizeMembers(input.members).filter((member) => ['assignee', 'follower'].includes(member.role)) } }));
      break;
    case 'members-remove':
      response = await callRemote(() => client.task.v2.task.removeMembers({ path: { task_guid: taskGuid }, data: { members: normalizeMembers(input.members).map(({ id, type }) => ({ id, type })) } }));
      break;
    case 'reminder-add': {
      const minute = input.relativeFireMinute;
      if (!Number.isInteger(minute) || minute < -525600 || minute > 0) throw contractError('INVALID_INPUT', 'relativeFireMinute 必须位于 -525600 到 0。', false);
      response = await callRemote(() => client.task.v2.task.addReminders({ path: { task_guid: taskGuid }, data: { reminders: [{ relative_fire_minute: minute }] } }));
      break;
    }
    case 'reminder-remove':
      response = await callRemote(() => client.task.v2.task.removeReminders({ path: { task_guid: taskGuid }, data: { reminder_ids: [requireString(input.reminderId, 'reminderId', 128)] } }));
      break;
    default:
      throw contractError('INVALID_INPUT', `transition 不支持操作：${input.operation}`, false);
  }
  const data = ensureApiSuccess(response, input.operation);
  return { previousUpdatedAt: current.updated_at || '', task: normalizeTask(data.task) };
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
  return plan;
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
  if (input.dryRun) return { dryRun: true, networkCalled: false, mutationApplied: false, plan: dryRunPlan({ ...input, input: normalized }) };
  const client = createClient();
  let data;
  if (input.taskId === 'es.feishu.task.monitor') data = await executeMonitor(client, normalized);
  else if (input.taskId === 'es.feishu.task.dispatch') data = await executeDispatch(client, normalized, input.runId);
  else data = await executeTransition(client, normalized);
  return { dryRun: false, networkCalled: true, mutationApplied: input.taskId !== 'es.feishu.task.monitor', operation, data };
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
    baseResult.errors.push(JSON.stringify({ code: error.contractCode, message: redact(error.message), retryable: Boolean(error.retryable), details: error.details || null }));
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
