'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const MAX_PROMPT = 12000;
const MAX_OUTPUT = 200000;
const hash = value => crypto.createHash('sha256').update(value, 'utf8').digest('hex');
const fail = (code, message) => { const error = new Error(message); error.code = code; throw error; };
function readJson(file) { return JSON.parse(fs.readFileSync(file, 'utf8')); }
function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`, { flag: 'wx' });
}
function requireSafePath(value, name, root) {
  if (typeof value !== 'string' || !value.trim()) fail('INVALID_INPUT', `${name} is required.`);
  const full = path.resolve(value);
  const rootPath = path.resolve(root);
  if (full !== rootPath && !full.toLowerCase().startsWith(`${rootPath.toLowerCase()}${path.sep}`))
    fail('PATH_ESCAPE', `${name} escapes the allowed root.`);
  return full;
}
function requireProjectFile(value, name, root) {
  const full = requireSafePath(value, name, root);
  if (!fs.existsSync(full) || !fs.statSync(full).isFile()) fail('FILE_MISSING', `${name} is missing.`);
  return full;
}
function readRuntimeConfig(root) {
  const configPath = path.join(root, 'ES', 'Automation', 'Temp', 'DeepSeekHarness', 'runtime.local.json');
  if (!fs.existsSync(configPath)) fail('RUNTIME_CONFIG_MISSING', 'runtime.local.json is required; run the project installer first.');
  const config = readJson(configPath);
  if (config.declaration !== 'es-deepseek' || config.providerDeclaration !== 'es-deepseek' || config.schemaVersion !== 1) fail('RUNTIME_CONFIG_INVALID', 'runtime.local.json identity is invalid.');
  return config;
}
function resolveRuntime(root) {
  const config = readRuntimeConfig(root);
  const nodeBinary = process.env.ES_DEEPSEEK_NODE_PATH || process.env.ES_AUTOMATION_NODE_PATH || config.nodePath || '';
  const bundledDsh = path.join(__dirname, 'node_modules', '.bin', process.platform === 'win32' ? 'dsh.cmd' : 'dsh');
  const configuredDsh = process.env.DSH_EXECUTABLE || config.dshExecutable || bundledDsh;
  const dshHome = config.dshHome || '';
  const workspace = config.workspace || '';
  if (!path.isAbsolute(nodeBinary) || !path.isAbsolute(configuredDsh) || !path.isAbsolute(dshHome) || !path.isAbsolute(workspace))
    fail('RUNTIME_CONFIG_INVALID', 'DSH runtime paths must be absolute.');
  requireSafePath(dshHome, 'dshHome', root);
  requireSafePath(workspace, 'workspace', root);
  if (!['headless', 'sdk'].includes(String(config.profile || 'headless').toLowerCase())) fail('PROFILE_INVALID', 'DSH Profile is not managed.');
  if (!fs.existsSync(nodeBinary) || !fs.statSync(nodeBinary).isFile()) fail('NODE_UNAVAILABLE', 'Configured node.exe is missing.');
  if (!fs.existsSync(configuredDsh) || !fs.statSync(configuredDsh).isFile()) fail('DSH_UNAVAILABLE', 'Configured dsh executable is missing.');
  if (!path.resolve(configuredDsh).toLowerCase().startsWith(path.resolve(root).toLowerCase() + path.sep))
    fail('PATH_ESCAPE', 'DSH executable must remain inside the project root.');
  const dshEntrypoint = configuredDsh.toLowerCase().endsWith('.cmd')
    ? path.join(path.dirname(path.dirname(configuredDsh)), '@deepseek-ai', 'dsh', 'lib', 'bin.js')
    : configuredDsh;
  const fallbackEntrypoint = path.join(__dirname, 'node_modules', '@deepseek-ai', 'dsh', 'lib', 'bin.js');
  const resolvedEntrypoint = fs.existsSync(dshEntrypoint) ? dshEntrypoint : fallbackEntrypoint;
  if (!fs.existsSync(resolvedEntrypoint) || !fs.statSync(resolvedEntrypoint).isFile()) fail('DSH_UNAVAILABLE', 'DSH JavaScript entrypoint is missing.');
  if (!path.resolve(resolvedEntrypoint).toLowerCase().startsWith(path.resolve(root).toLowerCase() + path.sep))
    fail('PATH_ESCAPE', 'DSH JavaScript entrypoint must remain inside the project root.');
  return { nodeBinary: path.resolve(nodeBinary), dshExecutable: path.resolve(configuredDsh), dshEntrypoint: path.resolve(resolvedEntrypoint), dshHome: path.resolve(dshHome), workspace: path.resolve(workspace), profile: config.profile || 'headless' };
}
function runProcess(fileName, args, cwd, timeoutMs, environment) {
  return new Promise((resolve, reject) => {
    const child = spawn(fileName, args, { cwd, shell: false, windowsHide: true, env: environment || process.env });
    let stdout = ''; let stderr = ''; let settled = false;
    const finish = (error, value) => { if (settled) return; settled = true; clearTimeout(timer); error ? reject(error) : resolve(value); };
    const timer = setTimeout(() => { try { child.kill(); } catch (_) {} const error = new Error('DeepSeek Harness timed out.'); error.code = 'TIMEOUT'; finish(error); }, timeoutMs);
    child.stdout.on('data', chunk => { stdout += chunk.toString('utf8'); if (stdout.length > MAX_OUTPUT) { try { child.kill(); } catch (_) {} const error = new Error('DeepSeek Harness output exceeded the bound.'); error.code = 'OUTPUT_LIMIT'; finish(error); } });
    child.stderr.on('data', chunk => { stderr += chunk.toString('utf8'); if (stderr.length > 12000) stderr = stderr.slice(-12000); });
    child.on('error', error => { error.code = error.code || 'PROCESS_START_FAILED'; finish(error); });
    child.on('close', code => { const value = { code, stdout: stdout.trim(), stderr: stderr.trim() }; if (code !== 0) { const error = new Error(value.stderr || `process exited with ${code}`); error.code = code === 127 ? 'PROCESS_UNAVAILABLE' : 'HARNESS_FAILED'; return finish(error); } finish(null, value); });
  });
}
async function probeRuntime(runtime, root) {
  const environment = { ...process.env, DSH_HOME: runtime.dshHome };
  const node = await runProcess(runtime.nodeBinary, ['--version'], root, 5000, environment);
  const match = /v(\d+)(?:\.\d+){1,2}/.exec(node.stdout || node.stderr || '');
  if (!match || Number(match[1]) < 22) fail('NODE_VERSION_UNSUPPORTED', 'Node.js 22 or newer is required.');
  const dsh = await runProcess(runtime.nodeBinary, [runtime.dshEntrypoint, '--profile', 'headless', '--help'], root, 30000, environment);
  return { nodeVersion: node.stdout || node.stderr, dshProbe: dsh.stdout || dsh.stderr, profile: runtime.profile };
}
async function runDsh(prompt, runtime, root, timeoutMs) {
  fs.mkdirSync(runtime.dshHome, { recursive: true });
  fs.mkdirSync(runtime.workspace, { recursive: true });
  const environment = { ...process.env, DSH_HOME: runtime.dshHome };
  const result = await runProcess(runtime.nodeBinary, [runtime.dshEntrypoint, '--profile', 'headless', prompt], runtime.workspace, timeoutMs, environment);
  return result.stdout;
}
async function main() {
  const inputArgument = process.argv[2] || '';
  const outputArgument = process.argv[3] || '';
  if (!inputArgument || !outputArgument) fail('INVALID_INPUT', 'inputPath/outputDirectory are required.');
  const inputPath = path.resolve(inputArgument);
  const outputDir = path.resolve(outputArgument);
  if (!fs.existsSync(inputPath)) fail('INVALID_INPUT', 'inputPath/outputDirectory are required.');
  const request = readJson(inputPath);
  const projectRoot = path.resolve(__dirname, '..', '..', '..', '..', '..');
  const root = path.resolve(request.projectRoot || projectRoot);
  if (root !== projectRoot) fail('PROJECT_ROOT_MISMATCH', 'projectRoot must match the project that owns this Worker.');
  if (!path.resolve(inputPath).toLowerCase().startsWith(`${projectRoot.toLowerCase()}${path.sep}`)) fail('PATH_ESCAPE', 'inputPath must remain inside the owning project.');
  if (request.providerDeclaration !== 'es-deepseek' || request.workerId !== 'es.deepseek-harness' || request.workerVersion !== '0.2.0') fail('IDENTITY_MISMATCH', 'Worker identity is invalid.');
  if (request.taskId !== 'es.deepseek.harness') fail('IDENTITY_MISMATCH', 'Task identity is invalid.');
  if (request.entrypointHash && request.entrypointHash !== hash(fs.readFileSync(__filename))) fail('SOURCE_DRIFT', 'Worker entrypoint hash mismatch.');
  const operation = request.operation || (request.dryRun ? 'dry-run' : 'headless-prompt');
  if (!['dry-run', 'check-local', 'headless-prompt'].includes(operation)) fail('INVALID_INPUT', 'operation is not registered.');
  const prompt = typeof request.prompt === 'string' ? request.prompt : '';
  if (operation !== 'check-local' && (prompt.length < 1 || prompt.length > MAX_PROMPT)) fail('INVALID_INPUT', 'prompt length is out of bounds.');
  const outputRoot = path.join(root, 'ES', 'Automation', 'Runs');
  const outputPath = path.join(requireSafePath(outputDir, 'outputDirectory', outputRoot), 'deepseek-harness-output.json');
  const startedAtUtc = new Date().toISOString();
  const result = { protocolVersion: 1, taskId: 'es.deepseek.harness', taskVersion: 1, runId: request.runId || '', workerType: 'Other', workerId: 'es.deepseek-harness', workerVersion: '0.2.0', entrypointHash: hash(fs.readFileSync(__filename)), status: 'Failed', exitCode: 1, startedAtUtc, finishedAtUtc: '', inputManifestHash: hash(fs.readFileSync(inputPath)), outputs: [], outputHashes: [], findings: [], errors: [], completionDecision: null };
  try {
    if (operation === 'dry-run' || request.dryRun) {
      writeJson(outputPath, { frameworkId: 'deepseek-harness', declaration: 'es-deepseek', role: 'external-execution-plane', authority: 'ESFramework/ESAI', authorityLevel: 'high-contributor-not-final-acceptance', status: 'dry-run', networkCalled: false, mutationApplied: false, promptHash: hash(prompt), provider: 'not-selected', flow: ['ES task authorization', 'DSH dry-run projection', 'ES evidence/acceptance remains authoritative'] });
      result.status = 'DryRun'; result.exitCode = 0; result.findings.push('DeepSeek Harness dry-run completed without process start.');
    } else {
      const runtime = resolveRuntime(root);
      const probe = await probeRuntime(runtime, root);
      if (operation === 'check-local') {
        const providerConfigured = Boolean(process.env.DEEPSEEK_API_KEY);
        const requireProvider = request.requireProvider === true;
        const connected = Boolean(probe.dshProbe) && (!requireProvider || providerConfigured);
        writeJson(outputPath, { frameworkId: 'deepseek-harness', declaration: 'es-deepseek', role: 'external-execution-plane', authority: 'ESFramework/ESAI', authorityLevel: 'high-contributor-not-final-acceptance', status: connected ? 'connected' : 'not-connected', networkCalled: false, mutationApplied: false, providerConfigured, runtime: { nodeVersion: probe.nodeVersion, profile: probe.profile }, flow: ['本地 Node/DSH/Profile 探测', '凭据存在性检查（不读取值）', 'ES 保留最终接受权'] });
        if (!connected) { const error = new Error(providerConfigured ? 'DSH runtime probe failed.' : 'DEEPSEEK_API_KEY is not configured.'); error.code = providerConfigured ? 'DSH_NOT_CONNECTED' : 'PROVIDER_UNAVAILABLE'; throw error; }
        result.status = 'Passed'; result.exitCode = 0; result.findings.push('DeepSeek Harness local chain is connected.');
      } else {
        const answer = await runDsh(prompt, runtime, root, Math.min(7200000, Math.max(1000, Number(request.timeoutMs) || 600000)));
        writeJson(outputPath, { frameworkId: 'deepseek-harness', declaration: 'es-deepseek', role: 'external-execution-plane', authority: 'ESFramework/ESAI', authorityLevel: 'high-contributor-not-final-acceptance', status: 'invoked', networkCalled: true, mutationApplied: false, promptHash: hash(prompt), outputHash: hash(answer), provider: 'deepseek-harness', answer, flow: ['ES authorized TaskContract', 'DSH headless agent loop', 'ES collects evidence and decides completion'] });
        result.status = 'Passed'; result.exitCode = 0; result.findings.push('DeepSeek Harness invocation completed; ES remains completion authority.');
      }
    }
    result.outputs = [path.relative(outputDir, outputPath).replaceAll(path.sep, '/')]; result.outputHashes = [hash(fs.readFileSync(outputPath))];
  } catch (error) {
    result.errors.push(JSON.stringify({ code: error.code || 'HARNESS_FAILED', message: String(error.message).slice(0, 1000) }));
  }
  result.finishedAtUtc = new Date().toISOString();
  writeJson(path.join(outputDir, 'result.json'), result);
  process.exitCode = result.exitCode;
}
main().catch(error => { process.stderr.write(`${JSON.stringify({ code: error.code || 'WORKER_FAILED', message: String(error.message).slice(0, 1000) })}\n`); process.exitCode = 1; });
