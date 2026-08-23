'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const lark = require('@larksuiteoapi/node-sdk');

const utf8 = 'utf8';

function sha256Buffer(buffer) {
  return crypto.createHash('sha256').update(buffer).digest('hex');
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

function normalizePageSize(value) {
  if (value === undefined || value === null) return 20;
  if (!Number.isInteger(value) || value < 1 || value > 50) {
    throw new Error('pageSize 必须是 1 到 50 的整数。');
  }
  return value;
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
    const message = response && response.msg ? response.msg : '没有返回错误详情';
    throw new Error(`${operation} 失败：${code} ${message}`);
  }
  return response.data || {};
}

async function executeOperation(input) {
  if (input.dryRun) {
    return {
      dryRun: true,
      operation: input.operation,
      validated: true,
      networkCalled: false,
    };
  }

  const client = createClient();
  switch (input.operation) {
    case 'auth-status': {
      const response = await client.wiki.v2.space.list({ params: { page_size: 1 } });
      const data = ensureApiSuccess(response, 'auth-status');
      return {
        authenticated: true,
        accessibleSpaceCountInPage: Array.isArray(data.items) ? data.items.length : 0,
        hasMore: Boolean(data.has_more),
      };
    }
    case 'knowledge-search': {
      const query = requireString(input.query, 'query');
      const pageSize = normalizePageSize(input.pageSize);
      const response = await client.wiki.v1.node.search({
        data: {
          query,
          ...(input.spaceId ? { space_id: requireString(input.spaceId, 'spaceId') } : {}),
        },
        params: { page_size: pageSize },
      });
      const data = ensureApiSuccess(response, 'knowledge-search');
      return {
        query,
        items: Array.isArray(data.items) ? data.items : [],
        hasMore: Boolean(data.has_more),
        pageToken: data.page_token || '',
      };
    }
    case 'document-pull': {
      const documentId = requireString(input.documentId, 'documentId');
      const infoResponse = await client.docx.v1.document.get({ path: { document_id: documentId } });
      const contentResponse = await client.docx.v1.document.rawContent({ path: { document_id: documentId } });
      const info = ensureApiSuccess(infoResponse, 'document-pull/info');
      const content = ensureApiSuccess(contentResponse, 'document-pull/content');
      return {
        documentId,
        title: info.document && info.document.title ? info.document.title : '',
        revisionId: info.document && Number.isInteger(info.document.revision_id)
          ? info.document.revision_id
          : null,
        content: typeof content.content === 'string' ? content.content : '',
      };
    }
    default:
      throw new Error(`不支持的只读 Feishu 操作：${input.operation}`);
  }
}

async function main() {
  const inputPath = path.resolve(requireString(process.argv[2], 'inputPath'));
  const outputDirectory = path.resolve(requireString(process.argv[3], 'outputDirectory'));
  const startedAtUtc = new Date().toISOString();
  const input = JSON.parse(fs.readFileSync(inputPath, utf8));
  const inputManifestHash = sha256File(inputPath);
  const resultPath = path.join(outputDirectory, 'result.json');
  const dataPath = path.join(outputDirectory, 'feishu-data.json');

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
    const data = await executeOperation(input);
    writeJsonAtomic(dataPath, data);
    baseResult.status = input.dryRun ? 'DryRun' : 'Passed';
    baseResult.exitCode = 0;
    baseResult.outputs.push(dataPath);
    baseResult.outputHashes.push(sha256File(dataPath));
    baseResult.findings.push(input.dryRun
      ? '只读 Feishu 请求已完成 DryRun；未访问网络。'
      : `只读 Feishu 操作已完成：${input.operation}`);
  } catch (error) {
    baseResult.status = error && /ES_FEISHU_APP_/.test(String(error.message)) ? 'Blocked' : 'Failed';
    baseResult.errors.push(error instanceof Error ? error.message : String(error));
  }

  baseResult.finishedAtUtc = new Date().toISOString();
  writeJsonAtomic(resultPath, baseResult);
  process.exitCode = baseResult.exitCode;
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`);
  process.exitCode = 1;
});
