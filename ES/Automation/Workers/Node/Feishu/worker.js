'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const lark = require('@larksuiteoapi/node-sdk');

const utf8 = 'utf8';
const classification = 'ExternalCollaboration';
const sanitizerVersion = 'es-feishu-sanitizer-v1';
const credentialSourceType = 'managed-process-environment';
const maxTitleBytes = 2048;
const maxDocumentBytes = 256 * 1024;

function sha256Buffer(buffer) {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

function sha256String(value) {
  return sha256Buffer(Buffer.from(String(value), utf8));
}

function sha256File(filePath) {
  return sha256Buffer(fs.readFileSync(filePath));
}

function writeJsonAtomic(filePath, value) {
  const directory = path.dirname(filePath);
  fs.mkdirSync(directory, { recursive: true });
  const temporary = `${filePath}.${process.pid}.tmp`;
  fs.writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, { encoding: utf8, flag: 'wx' });
  fs.renameSync(temporary, filePath);
}

function requireString(value, field) {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new Error(`${field} 必须是非空字符串。`);
  }
  return value.trim();
}

function requireHash(value, field) {
  const normalized = requireString(value, field);
  if (!/^[a-f0-9]{64}$/i.test(normalized)) throw new Error(`${field} 必须是 SHA-256。`);
  return normalized.toLowerCase();
}

function requireIdentifier(value, field) {
  const normalized = requireString(value, field);
  if (!/^[A-Za-z0-9_-]{1,128}$/.test(normalized)) throw new Error(`${field} 格式无效。`);
  return normalized;
}

function normalizeObjectType(value) {
  if (!Number.isSafeInteger(value) || value < 0 || value > 65535) {
    throw new Error('obj_type 格式无效。');
  }
  return String(value);
}

function normalizePageSize(value) {
  if (value === undefined || value === null) return 20;
  if (!Number.isInteger(value) || value < 1 || value > 50) {
    throw new Error('pageSize 必须是 1 到 50 的整数。');
  }
  return value;
}

function truncateUtf8(value, maxBytes) {
  let result = '';
  let bytes = 0;
  let truncated = false;
  for (const character of value) {
    const characterBytes = Buffer.byteLength(character, utf8);
    if (bytes + characterBytes > maxBytes) {
      truncated = true;
      break;
    }
    result += character;
    bytes += characterBytes;
  }
  return { value: result, bytes, truncated };
}

function sanitizeText(value, maxBytes) {
  let text = typeof value === 'string' ? value.normalize('NFKC') : '';
  let redactionCount = 0;
  const replace = (pattern, replacement) => {
    text = text.replace(pattern, () => {
      redactionCount += 1;
      return replacement;
    });
  };
  replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/g, '');
  replace(/-----BEGIN [^-]*(?:PRIVATE KEY)-----[\s\S]*?-----END [^-]*(?:PRIVATE KEY)-----/gi,
    '<redacted-private-key>');
  replace(/\bBearer\s+[A-Za-z0-9._~+/-]+=*/gi, 'Bearer <redacted>');
  replace(/\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b/g,
    '<redacted-token>');
  replace(/\b(authorization|cookie|app[_-]?secret|access[_-]?token|refresh[_-]?token)\s*[:=]\s*[^\s,;]+/gi,
    '<redacted-sensitive-value>');
  const bounded = truncateUtf8(text, maxBytes);
  return {
    value: bounded.value,
    bytes: bounded.bytes,
    truncated: bounded.truncated,
    redactionCount,
  };
}

function sanitizeError(error) {
  return sanitizeText(error instanceof Error ? error.message : String(error), 2048).value;
}

function normalizeRemoteTime(value) {
  if (value === undefined || value === null || value === '') return '';
  const numeric = Number(value);
  const date = Number.isFinite(numeric)
    ? new Date(numeric > 100000000000 ? numeric : numeric * 1000)
    : new Date(String(value));
  return Number.isNaN(date.getTime()) ? '' : date.toISOString();
}

function parseAllowedSpaces() {
  const raw = process.env.ES_FEISHU_ALLOWED_SPACE_IDS || '';
  const values = raw.split(/[,;]/).map((value) => value.trim()).filter(Boolean);
  if (values.some((value) => !/^[A-Za-z0-9_-]{1,128}$/.test(value))) {
    throw new Error('ES_FEISHU_ALLOWED_SPACE_IDS 包含无效标识。');
  }
  return [...new Set(values)].sort();
}

function createRuntimeContext(input) {
  if (input.dryRun) {
    return {
      tenantHash: '',
      spacePolicyHash: '',
      appIdentityHash: '',
      allowedSpaces: [],
    };
  }
  const tenantId = requireString(process.env.ES_FEISHU_TENANT_ID, 'ES_FEISHU_TENANT_ID');
  const appId = requireString(process.env.ES_FEISHU_APP_ID, 'ES_FEISHU_APP_ID');
  const allowedSpaces = parseAllowedSpaces();
  if (input.operation !== 'auth-status') {
    const spaceId = requireIdentifier(input.spaceId, 'spaceId');
    if (!allowedSpaces.includes(spaceId)) throw new Error('SPACE_NOT_ALLOWED');
  }
  return {
    tenantHash: sha256String(tenantId),
    spacePolicyHash: sha256String(allowedSpaces.length === 0
      ? 'auth-status-only' : allowedSpaces.join('\n')),
    appIdentityHash: sha256String(appId),
    allowedSpaces,
  };
}

function createClient() {
  const appId = requireString(process.env.ES_FEISHU_APP_ID, 'ES_FEISHU_APP_ID');
  const appSecret = requireString(process.env.ES_FEISHU_APP_SECRET, 'ES_FEISHU_APP_SECRET');
  return new lark.Client({
    appId,
    appSecret,
    appType: lark.AppType.SelfBuild,
    domain: lark.Domain.Feishu,
    loggerLevel: lark.LoggerLevel.error,
  });
}

function ensureApiSuccess(response, operation) {
  if (!response || (response.code !== undefined && response.code !== 0)) {
    const code = response && response.code !== undefined ? response.code : 'Unknown';
    const message = response && response.msg ? sanitizeText(response.msg, 512).value : '无错误详情';
    throw new Error(`${operation} 失败：${code} ${message}`);
  }
  return response.data || {};
}

function createSourceRef(context, values) {
  return {
    provider: 'feishu',
    tenantHash: context.tenantHash,
    spaceIdHash: sha256String(values.spaceId),
    objectType: String(values.objectType),
    objectTokenHash: sha256String(values.objectToken),
    remoteVersion: values.remoteVersion === undefined || values.remoteVersion === null
      ? '' : String(values.remoteVersion),
    updatedAtUtc: normalizeRemoteTime(values.updatedAt),
    retrievedAtUtc: values.retrievedAtUtc,
    contentHash: values.contentHash || '',
    classification,
    sanitizerVersion,
  };
}

function withGovernanceMetadata(value, networkCalled, sourceRefs, redactionCount) {
  return {
    ...value,
    networkCalled,
    classification,
    sanitizerVersion,
    redactionCount,
    sourceRefs,
  };
}

async function executeOperation(input, context) {
  if (input.dryRun) {
    return withGovernanceMetadata({
      dryRun: true,
      operation: input.operation,
      validated: true,
    }, false, [], 0);
  }

  const client = createClient();
  const retrievedAtUtc = new Date().toISOString();
  switch (input.operation) {
    case 'auth-status': {
      const response = await client.wiki.v2.space.list({ params: { page_size: 1 } });
      const data = ensureApiSuccess(response, 'auth-status');
      return withGovernanceMetadata({
        operation: input.operation,
        authenticated: true,
        accessibleSpaceCountInPage: Array.isArray(data.items) ? data.items.length : 0,
        hasMore: Boolean(data.has_more),
        tenantHash: context.tenantHash,
        appIdentityHash: context.appIdentityHash,
        checkedAtUtc: retrievedAtUtc,
      }, true, [], 0);
    }
    case 'knowledge-search': {
      const query = requireString(input.query, 'query');
      if ([...query].length > 512) throw new Error('query 最多允许 512 个字符。');
      const spaceId = requireIdentifier(input.spaceId, 'spaceId');
      const pageSize = normalizePageSize(input.pageSize);
      const response = await client.wiki.v1.node.search({
        data: { query, space_id: spaceId },
        params: { page_size: pageSize },
      });
      const data = ensureApiSuccess(response, 'knowledge-search');
      const rawItems = Array.isArray(data.items) ? data.items : [];
      if (rawItems.length > pageSize) throw new Error('REMOTE_RESPONSE_EXCEEDS_PAGE_SIZE');
      const sourceRefs = [];
      let redactionCount = 0;
      const items = rawItems.map((item) => {
        if (!item || item.space_id !== spaceId) throw new Error('SPACE_NOT_ALLOWED');
        const title = sanitizeText(item.title, maxTitleBytes);
        redactionCount += title.redactionCount;
        const contentHash = sha256String(title.value);
        const objectType = normalizeObjectType(item.obj_type);
        const sourceRef = createSourceRef(context, {
          spaceId,
          objectType,
          objectToken: requireIdentifier(item.obj_token, 'obj_token'),
          remoteVersion: item.version,
          updatedAt: item.update_time,
          retrievedAtUtc,
          contentHash,
        });
        sourceRefs.push(sourceRef);
        return {
          sourceRef,
          objectType,
          title: title.value,
          titleTruncated: title.truncated,
          updatedAtUtc: normalizeRemoteTime(item.update_time),
          version: Number.isInteger(item.version) ? item.version : null,
          urlRefHash: typeof item.url === 'string' && item.url.length > 0
            ? sha256String(item.url) : '',
          classification,
        };
      });
      return withGovernanceMetadata({
        operation: input.operation,
        queryHash: sha256String(query),
        items,
        hasMore: Boolean(data.has_more),
        nextPageTokenHash: data.page_token ? sha256String(data.page_token) : '',
        pagesRead: 1,
        truncated: Boolean(data.has_more),
        retrievedAtUtc,
      }, true, sourceRefs, redactionCount);
    }
    case 'document-pull': {
      const spaceId = requireIdentifier(input.spaceId, 'spaceId');
      const documentId = requireIdentifier(input.documentId, 'documentId');
      const nodeResponse = await client.wiki.v2.space.getNode({
        params: { token: documentId, obj_type: 'docx' },
      });
      const nodeData = ensureApiSuccess(nodeResponse, 'document-pull/node');
      const node = nodeData.node || {};
      if (node.space_id !== spaceId || node.obj_token !== documentId || node.obj_type !== 'docx') {
        throw new Error('SPACE_NOT_ALLOWED');
      }
      const infoResponse = await client.docx.v1.document.get({ path: { document_id: documentId } });
      const contentResponse = await client.docx.v1.document.rawContent({ path: { document_id: documentId } });
      const info = ensureApiSuccess(infoResponse, 'document-pull/info');
      const content = ensureApiSuccess(contentResponse, 'document-pull/content');
      const title = sanitizeText(info.document && info.document.title ? info.document.title : node.title,
        maxTitleBytes);
      const body = sanitizeText(typeof content.content === 'string' ? content.content : '',
        maxDocumentBytes);
      const contentHash = sha256String(body.value);
      const sourceRef = createSourceRef(context, {
        spaceId,
        objectType: 'docx',
        objectToken: documentId,
        remoteVersion: info.document && Number.isInteger(info.document.revision_id)
          ? info.document.revision_id : '',
        updatedAt: node.obj_edit_time,
        retrievedAtUtc,
        contentHash,
      });
      return withGovernanceMetadata({
        operation: input.operation,
        sourceRef,
        title: title.value,
        titleTruncated: title.truncated,
        revisionId: info.document && Number.isInteger(info.document.revision_id)
          ? info.document.revision_id : null,
        updatedAtUtc: normalizeRemoteTime(node.obj_edit_time),
        retrievedAtUtc,
        content: body.value,
        contentHash,
        contentBytes: body.bytes,
        truncated: body.truncated,
      }, true, [sourceRef], title.redactionCount + body.redactionCount);
    }
    default:
      throw new Error(`不支持的只读 Feishu 操作：${input.operation}`);
  }
}

function validateInput(input) {
  const allowedFields = new Set([
    'protocolVersion', 'taskId', 'taskVersion', 'runId', 'workerType', 'workerId',
    'workerVersion', 'entrypointHash', 'commandId', 'planHash', 'governanceHash',
    'invocationHash', 'dryRun', 'operation', 'query', 'spaceId', 'documentId', 'pageSize',
    'runtimeAuthorizationRef', 'credentialSourceType', 'tenantHash', 'spacePolicyHash',
  ]);
  for (const field of Object.keys(input)) {
    if (!allowedFields.has(field)) throw new Error(`未注册输入字段：${field}`);
  }
  requireHash(input.planHash, 'planHash');
  requireHash(input.governanceHash, 'governanceHash');
  requireHash(input.invocationHash, 'invocationHash');
  if (input.protocolVersion !== 1 || input.taskId !== 'es.feishu.read'
    || input.taskVersion !== 1 || input.workerType !== 'Other'
    || input.workerId !== 'es.feishu.node' || input.workerVersion !== '0.1.0'
    || input.commandId !== 'feishu.read') {
    throw new Error('受管 Feishu Worker 身份无效。');
  }
  if (!/^[a-f0-9]{32}$/i.test(requireString(input.runId, 'runId'))) throw new Error('runId 格式无效。');
  if (!['auth-status', 'knowledge-search', 'document-pull'].includes(input.operation)) {
    throw new Error('operation 不在只读白名单中。');
  }
  if (typeof input.dryRun !== 'boolean') throw new Error('dryRun 必须是布尔值。');
  if (!input.dryRun) {
    requireHash(input.runtimeAuthorizationRef, 'runtimeAuthorizationRef');
    requireHash(input.tenantHash, 'tenantHash');
    requireHash(input.spacePolicyHash, 'spacePolicyHash');
    if (input.credentialSourceType !== credentialSourceType) throw new Error('credentialSourceType 无效。');
  }
}

async function main() {
  const inputPath = path.resolve(requireString(process.argv[2], 'inputPath'));
  const outputDirectory = path.resolve(requireString(process.argv[3], 'outputDirectory'));
  const projectRoot = path.resolve(__dirname, '..', '..', '..', '..', '..');
  const managedRoot = fs.realpathSync(path.join(projectRoot, 'ES', 'Automation', 'Temp', 'Feishu'));
  const resolvedOutput = fs.realpathSync(outputDirectory);
  const resolvedInput = fs.realpathSync(inputPath);
  const relativeOutput = path.relative(managedRoot, resolvedOutput);
  const relativeInput = path.relative(resolvedOutput, resolvedInput);
  if (!/^[a-f0-9]{32}$/i.test(relativeOutput) || path.isAbsolute(relativeOutput)
    || relativeInput !== 'request.json') {
    throw new Error('Worker 输入或输出路径越过受管 Feishu Run 目录。');
  }
  const startedAtUtc = new Date().toISOString();
  const input = JSON.parse(fs.readFileSync(inputPath, utf8));
  validateInput(input);
  if (input.runId.toLowerCase() !== path.basename(resolvedOutput).toLowerCase()) {
    throw new Error('runId 与受管 Feishu Run 目录不一致。');
  }
  if (input.entrypointHash !== sha256File(__filename)) throw new Error('Worker 入口 Hash 漂移。');
  const inputManifestHash = sha256File(inputPath);
  const resultPath = path.join(outputDirectory, 'result.json');
  const dataPath = path.join(outputDirectory, 'feishu-data.json');
  const receiptPath = path.join(outputDirectory, 'feishu-receipt.json');

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
    const context = createRuntimeContext(input);
    if (!input.dryRun
      && (context.tenantHash !== input.tenantHash
        || context.spacePolicyHash !== input.spacePolicyHash)) {
      throw new Error('Runtime tenant or space policy hash mismatch.');
    }
    const data = await executeOperation(input, context);
    writeJsonAtomic(dataPath, data);
    const dataHash = sha256File(dataPath);
    const completedAtUtc = new Date().toISOString();
    const receipt = {
      protocolVersion: 1,
      planHash: input.planHash,
      commandId: input.commandId,
      taskId: input.taskId,
      taskVersion: input.taskVersion,
      governanceHash: input.governanceHash,
      dryRun: input.dryRun,
      operation: input.operation,
      runId: input.runId,
      invocationHash: input.invocationHash,
      inputManifestHash,
      outputHashes: [dataHash],
      evidenceScope: input.dryRun ? 'Static' : 'Runtime',
      classification,
      sanitizerVersion,
      networkCalled: data.networkCalled,
      exitCode: 0,
      startedAtUtc,
      completedAtUtc,
      runtimeAuthorizationRef: input.dryRun ? '' : input.runtimeAuthorizationRef,
      credentialSourceType: input.dryRun ? '' : credentialSourceType,
      tenantHash: input.dryRun ? '' : context.tenantHash,
      spacePolicyHash: input.dryRun ? '' : context.spacePolicyHash,
      redactionCount: data.redactionCount,
      sourceRefs: data.sourceRefs,
      unresolvedGaps: input.dryRun
        ? ['runtime-not-run', 'feishu-authentication-not-proven', 'network-not-called']
        : ['vendor-idempotency-not-proven', 'domain-reload-recovery-not-proven'],
    };
    writeJsonAtomic(receiptPath, receipt);
    baseResult.status = input.dryRun ? 'DryRun' : 'Passed';
    baseResult.exitCode = 0;
    baseResult.outputs.push(dataPath, receiptPath);
    baseResult.outputHashes.push(dataHash, sha256File(receiptPath));
    baseResult.findings.push(input.dryRun
      ? 'Feishu 只读请求已完成 DryRun；未访问网络。'
      : `Feishu 只读操作已完成并生成脱敏回执：${input.operation}`);
  } catch (error) {
    baseResult.status = error && /ES_FEISHU_APP_|ES_FEISHU_TENANT_ID|SPACE_NOT_ALLOWED/.test(String(error.message))
      ? 'Blocked' : 'Failed';
    baseResult.errors.push(sanitizeError(error));
  }

  baseResult.finishedAtUtc = new Date().toISOString();
  writeJsonAtomic(resultPath, baseResult);
  process.exitCode = baseResult.exitCode;
}

main().catch((error) => {
  process.stderr.write(`${sanitizeError(error)}\n`);
  process.exitCode = 1;
});
