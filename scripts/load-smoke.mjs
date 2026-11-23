import { performance } from 'node:perf_hooks';

const [baseValue = 'http://127.0.0.1:8080', searchPath, countValue = '240', workersValue = '20'] = process.argv.slice(2);
const base = new URL(baseValue);
const total = Number(countValue);
const concurrency = Number(workersValue);
if (!['127.0.0.1', 'localhost', '[::1]'].includes(base.hostname)
    || !['http:', 'https:'].includes(base.protocol) || !searchPath?.startsWith('/chuyen-bay?')
    || !Number.isInteger(total) || total < 1 || total > 2000
    || !Number.isInteger(concurrency) || concurrency < 1 || concurrency > 80) {
    throw new Error('Usage: node scripts/load-smoke.mjs http://127.0.0.1:8080 "/chuyen-bay?from=SGN&to=HAN&depart=YYYY-MM-DD" [requests] [concurrency]');
}

const journeys = [
    ['search', searchPath], ['search', searchPath], ['search', searchPath], ['search', searchPath],
    ['home', '/'], ['lookup', '/tra-cuu'], ['health', '/health/database']
];

async function request(label, path) {
    const start = performance.now();
    let success = false;
    try {
        const response = await fetch(new URL(path, base), { redirect: 'manual', signal: AbortSignal.timeout(10000) });
        const body = await response.text();
        const marker = label === 'search' ? /data-result-count="[1-9]\d*"/.test(body)
            : label === 'health' ? body.includes('"status":"connected"') : body.length > 100;
        success = response.status === 200 && marker;
    } catch {
        // Timeouts and network errors are failures and are included in timings.
    }
    return { label, success, ms: performance.now() - start };
}

const warm = await request('search', searchPath);
if (!warm.success) throw new Error('Use a search date and route with at least one available flight.');

let next = 0;
const results = [];
const start = performance.now();
await Promise.all(Array.from({ length: concurrency }, async () => {
    while (next < total) {
        const [label, path] = journeys[next++ % journeys.length];
        results.push(await request(label, path));
    }
}));
const elapsed = (performance.now() - start) / 1000;
const summarize = rows => {
    const times = rows.map(row => row.ms).sort((left, right) => left - right);
    const percentile = n => Math.round(times[Math.ceil(times.length * n) - 1]);
    return { requests: rows.length, failures: rows.filter(row => !row.success).length, p50Ms: percentile(.5), p95Ms: percentile(.95), maxMs: percentile(1) };
};
const routes = Object.fromEntries([...new Set(journeys.map(([label]) => label))]
    .map(label => [label, summarize(results.filter(row => row.label === label))])
    .filter(([, value]) => value.requests > 0));
const overall = summarize(results);
console.log(JSON.stringify({ concurrency, elapsedSeconds: +elapsed.toFixed(2), requestsPerSecond: +(total / elapsed).toFixed(1), ...overall, routes }, null, 2));
if (overall.failures) process.exitCode = 1;
