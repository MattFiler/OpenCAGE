<?php

if (file_exists('../keys.php')) {
    require_once '../keys.php';
} else {
    http_response_code(500);
    echo 'Database keys file not found. Please create keys.php';
    exit;
}

$conn = new mysqli("localhost", $username, $password, "OpenCAGE");
if ($conn->connect_error) {
    die("Connection failed: " . $conn->connect_error);
}

function extract_crash_info($error_log) {
    $lines = preg_split("/\r\n|\r|\n/", (string)$error_log);
    $handler = '';
    $exception = 'Unknown';
    $message = '';

    foreach ($lines as $line) {
        $trimmed = trim($line);
        if ($trimmed === '') {
            continue;
        }

        if ($handler === '' && preg_match('/^(CurrentDomain_UnhandledException|Application_ThreadException)\b/', $trimmed)) {
            $handler = $trimmed;
            continue;
        }

        if (preg_match('/^((?:[\w.]+(?:Exception|Error))(?:\s*\([^)]+\))?)\s*:\s*(.*)$/', $trimmed, $m)) {
            $exception = $m[1];
            $message = trim($m[2]);
            break;
        }

        if (preg_match('/^(System\.[\w.]+(?:Exception|Error))/', $trimmed, $m)) {
            $exception = $m[1];
            if (preg_match('/:\s*(.+)$/', $trimmed, $m2)) {
                $message = trim($m2[1]);
            }
            break;
        }

        if ($exception === 'Unknown' && $handler === '' && strpos($trimmed, ' at ') !== 0) {
            $message = $trimmed;
        }
    }

    $shortMessage = $message;
    if (mb_strlen($shortMessage) > 120) {
        $shortMessage = mb_substr($shortMessage, 0, 117) . '...';
    }

    $type = $exception;
    if ($handler !== '') {
        $type = $handler . ' · ' . $exception;
    }

    $signature = $exception;
    if ($shortMessage !== '') {
        $signature .= ': ' . $shortMessage;
    }

    return [
        'handler' => $handler,
        'exception' => $exception,
        'message' => $shortMessage,
        'type' => $type,
        'signature' => $signature,
    ];
}

$sql = "SELECT id, error_log, application_version, game_version, datetime, uptime, os_name, cpu_name, ram_total, current_level, current_composite, current_entity FROM crashes ORDER BY datetime DESC";
$result = $conn->query($sql);

$crashes = [];
if ($result) {
    while ($row = $result->fetch_assoc()) {
        $info = extract_crash_info($row['error_log']);
        unset($row['error_log']);
        $row['crash_handler'] = $info['handler'];
        $row['crash_exception'] = $info['exception'];
        $row['crash_message'] = $info['message'];
        $row['crash_type'] = $info['type'];
        $row['crash_signature'] = $info['signature'];
        $crashes[] = $row;
    }
}

$conn->close();
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>OpenCAGE Crash Logs</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        :root {
            --bg: #eef2f5;
            --surface: #ffffff;
            --surface-2: #f7f9fb;
            --border: #d7dee5;
            --text: #1f2933;
            --muted: #617283;
            --accent: #0b6bcb;
            --accent-soft: #e7f1fb;
            --danger: #b42318;
            --shadow: 0 8px 24px rgba(31, 41, 51, 0.08);
            --radius: 12px;
        }

        * { box-sizing: border-box; }

        body {
            margin: 0;
            font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(180deg, #e8eef4 0%, var(--bg) 40%);
            color: var(--text);
            line-height: 1.45;
        }

        .page {
            max-width: 1280px;
            margin: 0 auto;
            padding: 24px 16px 48px;
        }

        .header {
            display: flex;
            flex-wrap: wrap;
            justify-content: space-between;
            gap: 12px;
            align-items: flex-end;
            margin-bottom: 20px;
        }

        .header h1 {
            margin: 0;
            font-size: 1.75rem;
            font-weight: 650;
            letter-spacing: -0.02em;
        }

        .header p {
            margin: 4px 0 0;
            color: var(--muted);
            font-size: 0.95rem;
        }

        .header a {
            color: var(--accent);
            text-decoration: none;
            font-weight: 600;
        }

        .header a:hover { text-decoration: underline; }

        .card {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: var(--radius);
            box-shadow: var(--shadow);
            padding: 16px;
            margin-bottom: 16px;
        }

        .card h2 {
            margin: 0 0 12px;
            font-size: 1.05rem;
            font-weight: 650;
        }

        .card-header-row {
            display: flex;
            flex-wrap: wrap;
            justify-content: space-between;
            gap: 8px;
            align-items: center;
            margin-bottom: 12px;
        }

        .card-header-row h2 { margin: 0; }

        .stats {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
            gap: 12px;
            margin-bottom: 16px;
        }

        .stat {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: var(--radius);
            padding: 14px 16px;
            box-shadow: var(--shadow);
        }

        .stat .label {
            color: var(--muted);
            font-size: 0.8rem;
            text-transform: uppercase;
            letter-spacing: 0.04em;
            font-weight: 600;
        }

        .stat .value {
            margin-top: 4px;
            font-size: 1.7rem;
            font-weight: 700;
            color: var(--accent);
        }

        .toolbar {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
            gap: 10px;
        }

        label.field {
            display: flex;
            flex-direction: column;
            gap: 6px;
            font-size: 0.8rem;
            color: var(--muted);
            font-weight: 600;
        }

        select, input[type="search"], button, .chip {
            font: inherit;
        }

        select, input[type="search"] {
            width: 100%;
            padding: 9px 10px;
            border: 1px solid var(--border);
            border-radius: 8px;
            background: #fff;
            color: var(--text);
        }

        select:focus, input[type="search"]:focus {
            outline: 2px solid rgba(11, 107, 203, 0.25);
            border-color: var(--accent);
        }

        .actions {
            display: flex;
            align-items: flex-end;
            gap: 8px;
        }

        button {
            border: 1px solid var(--border);
            background: var(--surface-2);
            color: var(--text);
            border-radius: 8px;
            padding: 9px 12px;
            cursor: pointer;
            font-weight: 600;
        }

        button:hover { background: #edf2f7; }
        button.primary {
            background: var(--accent);
            border-color: var(--accent);
            color: #fff;
        }
        button.primary:hover { filter: brightness(0.96); }
        button.linkish {
            background: transparent;
            border: none;
            color: var(--accent);
            padding: 0;
            font-weight: 650;
        }

        .timeline-meta {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 10px;
        }

        .timeline-meta .range-label {
            color: var(--muted);
            font-size: 0.9rem;
        }

        .timeline-meta strong { color: var(--text); }

        .timeline-track {
            position: relative;
            height: 64px;
            padding: 18px 8px 28px;
            user-select: none;
            touch-action: none;
        }

        .timeline-rail {
            position: absolute;
            left: 8px;
            right: 8px;
            top: 28px;
            height: 8px;
            border-radius: 999px;
            background: #d9e2ec;
        }

        .timeline-fill {
            position: absolute;
            top: 0;
            bottom: 0;
            border-radius: 999px;
            background: linear-gradient(90deg, #4d9de0, #0b6bcb);
        }

        .timeline-ticks {
            position: absolute;
            left: 8px;
            right: 8px;
            top: 8px;
            bottom: 0;
            display: flex;
            justify-content: space-between;
            pointer-events: none;
        }

        .timeline-tick {
            width: 1px;
            display: flex;
            flex-direction: column;
            align-items: center;
            color: var(--muted);
            font-size: 0.68rem;
        }

        .timeline-tick span.mark {
            width: 2px;
            height: 12px;
            background: #9fb3c8;
            margin-bottom: 6px;
        }

        .timeline-tick span.label {
            max-width: 72px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            transform: translateY(10px);
        }

        .timeline-handle {
            position: absolute;
            top: 20px;
            width: 20px;
            height: 20px;
            margin-left: -10px;
            border-radius: 50%;
            background: #fff;
            border: 3px solid var(--accent);
            box-shadow: 0 2px 8px rgba(11, 107, 203, 0.35);
            cursor: grab;
            z-index: 2;
        }

        .timeline-handle:active { cursor: grabbing; }

        .split {
            display: grid;
            grid-template-columns: 1.1fr 1fr;
            gap: 16px;
        }

        @media (max-width: 960px) {
            .split { grid-template-columns: 1fr; }
        }

        .table-wrap {
            overflow-x: auto;
            border: 1px solid var(--border);
            border-radius: 10px;
            background: #fff;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            min-width: 640px;
        }

        th, td {
            padding: 10px 12px;
            border-bottom: 1px solid var(--border);
            text-align: left;
            vertical-align: top;
            font-size: 0.9rem;
            word-break: break-word;
            overflow-wrap: anywhere;
        }

        th {
            background: var(--surface-2);
            color: var(--muted);
            font-size: 0.75rem;
            text-transform: uppercase;
            letter-spacing: 0.04em;
            position: sticky;
            top: 0;
            z-index: 1;
        }

        tbody tr:hover { background: #f8fbff; }

        .mono {
            font-family: Consolas, "Courier New", monospace;
            font-size: 0.84rem;
        }

        .muted { color: var(--muted); }

        .pill {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-width: 2.25rem;
            padding: 3px 9px;
            border-radius: 999px;
            background: var(--accent-soft);
            color: var(--accent);
            font-size: 0.8rem;
            font-weight: 700;
            white-space: nowrap;
            flex-shrink: 0;
        }

        .groups {
            display: flex;
            flex-direction: column;
            gap: 8px;
        }

        .group {
            border: 1px solid var(--border);
            border-radius: 10px;
            background: #fff;
            overflow: hidden;
        }

        .group summary {
            list-style: none;
            cursor: pointer;
            padding: 12px 14px;
            display: flex;
            gap: 12px;
            align-items: flex-start;
        }

        .group summary::-webkit-details-marker { display: none; }
        .group summary::marker { content: ''; }

        .group summary:hover { background: #f8fbff; }

        .group-main {
            flex: 1;
            min-width: 0;
        }

        .group-exception {
            font-size: 0.95rem;
            font-weight: 650;
            color: var(--text);
            line-height: 1.3;
        }

        .group-message {
            margin-top: 4px;
            color: var(--muted);
            font-size: 0.86rem;
            line-height: 1.4;
            overflow-wrap: anywhere;
            word-break: break-word;
        }

        .group-meta {
            margin-top: 6px;
            font-size: 0.78rem;
            color: #8a9aab;
        }

        .group-chevron {
            flex-shrink: 0;
            color: var(--muted);
            font-size: 1.1rem;
            line-height: 1;
            margin-top: 2px;
            transition: transform 0.15s ease;
        }

        .group[open] .group-chevron {
            transform: rotate(90deg);
        }

        .group[open] {
            border-color: #b7d3ef;
            box-shadow: inset 0 0 0 1px rgba(11, 107, 203, 0.08);
        }

        .group-body {
            border-top: 1px solid var(--border);
            background: var(--surface-2);
            max-height: 320px;
            overflow: auto;
        }

        .group-body table {
            min-width: 0;
            width: 100%;
        }

        .group-body th,
        .group-body td {
            font-size: 0.84rem;
            padding: 8px 12px;
        }

        .group-body th {
            position: sticky;
            top: 0;
            background: #eef3f8;
        }

        .group-more {
            padding: 10px 12px;
            font-size: 0.82rem;
            color: var(--muted);
            border-top: 1px solid var(--border);
        }

        .chart-box {
            position: relative;
            height: 280px;
        }

        .empty {
            padding: 24px;
            text-align: center;
            color: var(--muted);
        }

        .modal {
            display: none;
            position: fixed;
            inset: 0;
            z-index: 1000;
            background: rgba(15, 23, 32, 0.55);
            padding: 24px 12px;
            overflow: auto;
        }

        .modal.open { display: block; }

        .modal-dialog {
            max-width: 920px;
            margin: 4vh auto;
            background: #fff;
            border-radius: 14px;
            box-shadow: 0 20px 50px rgba(0, 0, 0, 0.25);
            border: 1px solid rgba(255, 255, 255, 0.2);
            overflow: hidden;
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 12px;
            padding: 14px 16px;
            border-bottom: 1px solid var(--border);
            background: var(--surface-2);
        }

        .modal-header h2 {
            margin: 0;
            font-size: 1.05rem;
        }

        .modal-body {
            padding: 16px;
        }

        .modal-body pre {
            margin: 0;
            white-space: pre-wrap;
            overflow-wrap: anywhere;
            word-break: break-word;
            background: #0f1720;
            color: #e7eef7;
            border-radius: 10px;
            padding: 14px;
            max-height: min(70vh, 720px);
            overflow: auto;
            font-size: 0.82rem;
            line-height: 1.45;
        }

        .close-x {
            width: 36px;
            height: 36px;
            border-radius: 8px;
            font-size: 1.4rem;
            line-height: 1;
            display: inline-flex;
            align-items: center;
            justify-content: center;
        }

        .crash-table td.narrow { white-space: nowrap; }
        .crash-table td.wrap { max-width: 220px; }

        .view-toggle {
            display: inline-flex;
            border: 1px solid var(--border);
            border-radius: 8px;
            overflow: hidden;
        }

        .view-toggle button {
            border: none;
            border-radius: 0;
            background: #fff;
        }

        .view-toggle button.active {
            background: var(--accent);
            color: #fff;
        }
    </style>
</head>
<body>
    <div class="page">
        <div class="header">
            <div>
                <h1>Crash Log Dashboard</h1>
                <p>Group, filter, and inspect OpenCAGE crash reports</p>
            </div>
            <a href="../">← Analytics</a>
        </div>

        <div class="stats" id="stats"></div>

        <div class="card">
            <div class="card-header-row">
                <h2>Filters</h2>
                <button type="button" id="resetFilters">Reset all</button>
            </div>
            <div class="toolbar" id="filters"></div>
        </div>

        <div class="card">
            <div class="card-header-row">
                <h2>Timeline range</h2>
                <label class="field" style="min-width:180px;margin:0;">
                    Dimension
                    <select id="timelineDimension">
                        <option value="application_version">App version</option>
                        <option value="os_name">OS</option>
                        <option value="cpu_name">CPU</option>
                        <option value="ram_total">RAM</option>
                    </select>
                </label>
            </div>
            <div class="timeline-meta">
                <div class="range-label" id="timelineRangeLabel">Loading…</div>
                <button type="button" id="resetTimeline">Reset range</button>
            </div>
            <div class="timeline-track" id="timelineTrack" aria-label="Timeline range filter">
                <div class="timeline-rail"><div class="timeline-fill" id="timelineFill"></div></div>
                <div class="timeline-ticks" id="timelineTicks"></div>
                <div class="timeline-handle" id="timelineHandleMin" role="slider" aria-label="Range start"></div>
                <div class="timeline-handle" id="timelineHandleMax" role="slider" aria-label="Range end"></div>
            </div>
        </div>

        <div class="split">
            <div class="card">
                <h2>Crashes over time</h2>
                <div class="chart-box">
                    <canvas id="crashesOverTimeChart"></canvas>
                </div>
            </div>
            <div class="card">
                <div class="card-header-row">
                    <h2>Crashes by version</h2>
                    <span class="muted" style="font-size:0.85rem;">Newest → oldest</span>
                </div>
                <div class="table-wrap" style="max-height:280px;overflow:auto;">
                    <table>
                        <thead>
                            <tr>
                                <th>App version</th>
                                <th>Game</th>
                                <th>Crashes</th>
                            </tr>
                        </thead>
                        <tbody id="versionTableBody"></tbody>
                    </table>
                </div>
            </div>
        </div>

        <div class="card">
            <div class="card-header-row">
                <h2 id="listTitle">Crashes by type</h2>
                <div class="view-toggle" role="group" aria-label="Crash list view">
                    <button type="button" class="active" data-view="grouped" id="viewGrouped">Grouped</button>
                    <button type="button" data-view="table" id="viewTable">Table</button>
                </div>
            </div>
            <div id="groupedView" class="groups"></div>
            <div id="tableView" class="table-wrap" style="display:none;max-height:640px;overflow:auto;">
                <table class="crash-table">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>When</th>
                            <th>Type</th>
                            <th>Version</th>
                            <th>OS / CPU / RAM</th>
                            <th>Context</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="crashTableBody"></tbody>
                </table>
            </div>
        </div>
    </div>

    <div id="errorModal" class="modal" aria-hidden="true">
        <div class="modal-dialog" role="dialog" aria-modal="true" aria-labelledby="modalTitle">
            <div class="modal-header">
                <h2 id="modalTitle">Crash log</h2>
                <button type="button" class="close-x" id="closeModalBtn" aria-label="Close">&times;</button>
            </div>
            <div class="modal-body">
                <pre id="errorLogContent">Loading…</pre>
            </div>
        </div>
    </div>

    <script>
        const ALL_CRASHES = <?php echo json_encode($crashes, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;

        const state = {
            search: '',
            application_version: '',
            game_version: '',
            os_name: '',
            cpu_name: '',
            ram_total: '',
            crash_exception: '',
            timelineDimension: 'application_version',
            timelineMin: 0,
            timelineMax: 0,
            timelineValues: [],
            view: 'grouped',
            chart: null,
        };

        function compareVersions(a, b) {
            const pa = String(a || '').split(/[^\d]+/).filter(Boolean).map(n => parseInt(n, 10) || 0);
            const pb = String(b || '').split(/[^\d]+/).filter(Boolean).map(n => parseInt(n, 10) || 0);
            const len = Math.max(pa.length, pb.length);
            for (let i = 0; i < len; i++) {
                const da = pa[i] || 0;
                const db = pb[i] || 0;
                if (da !== db) return da - db;
            }
            return String(a || '').localeCompare(String(b || ''));
        }

        function parseRamGb(value) {
            const m = String(value || '').match(/([\d.]+)\s*(GB|MB|TB)?/i);
            if (!m) return Number.NaN;
            let n = parseFloat(m[1]);
            const unit = (m[2] || 'GB').toUpperCase();
            if (unit === 'MB') n /= 1024;
            if (unit === 'TB') n *= 1024;
            return n;
        }

        function compareRam(a, b) {
            const ra = parseRamGb(a);
            const rb = parseRamGb(b);
            if (!Number.isNaN(ra) && !Number.isNaN(rb) && ra !== rb) return ra - rb;
            return String(a || '').localeCompare(String(b || ''), undefined, { sensitivity: 'base' });
        }

        function uniqueSorted(values, dimension) {
            const set = [...new Set(values.filter(v => v != null && String(v).trim() !== ''))];
            if (dimension === 'application_version') {
                return set.sort(compareVersions);
            }
            if (dimension === 'ram_total') {
                return set.sort(compareRam);
            }
            return set.sort((a, b) => String(a).localeCompare(String(b), undefined, { sensitivity: 'base' }));
        }

        function optionList(selector, values, selected) {
            const el = document.querySelector(selector);
            if (!el) return;
            el.innerHTML = '';

            const all = document.createElement('option');
            all.value = '';
            all.textContent = 'All';
            el.appendChild(all);

            values.forEach(v => {
                const opt = document.createElement('option');
                opt.value = String(v);
                opt.textContent = String(v);
                if (String(v) === String(selected || '')) opt.selected = true;
                el.appendChild(opt);
            });
        }

        function escapeHtml(str) {
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        function matchesFilters(crash, skipTimeline = false) {
            if (state.application_version && crash.application_version !== state.application_version) return false;
            if (state.game_version && crash.game_version !== state.game_version) return false;
            if (state.os_name && crash.os_name !== state.os_name) return false;
            if (state.cpu_name && crash.cpu_name !== state.cpu_name) return false;
            if (state.ram_total && crash.ram_total !== state.ram_total) return false;
            if (state.crash_exception && crash.crash_exception !== state.crash_exception) return false;

            if (!skipTimeline && state.timelineValues.length) {
                const value = crash[state.timelineDimension];
                const idx = state.timelineValues.indexOf(value);
                if (idx === -1 || idx < state.timelineMin || idx > state.timelineMax) return false;
            }

            if (state.search) {
                const q = state.search.toLowerCase();
                const hay = [
                    crash.id, crash.datetime, crash.uptime,
                    crash.application_version, crash.game_version,
                    crash.os_name, crash.cpu_name, crash.ram_total,
                    crash.current_level, crash.current_composite, crash.current_entity,
                    crash.crash_type, crash.crash_signature, crash.crash_exception, crash.crash_message
                ].join(' ').toLowerCase();
                if (!hay.includes(q)) return false;
            }

            return true;
        }

        function getFilteredCrashes() {
            return ALL_CRASHES.filter(c => matchesFilters(c));
        }

        function rebuildFilterOptions() {
            const base = ALL_CRASHES.filter(c => matchesFilters(c, true));

            optionList('#filterAppVersion', uniqueSorted(base.map(c => c.application_version), 'application_version').reverse(), state.application_version);
            optionList('#filterGameVersion', [...new Set(base.map(c => c.game_version).filter(Boolean))].sort(), state.game_version);
            optionList('#filterOs', [...new Set(base.map(c => c.os_name).filter(Boolean))].sort((a, b) => a.localeCompare(b)), state.os_name);
            optionList('#filterCpu', [...new Set(base.map(c => c.cpu_name).filter(Boolean))].sort((a, b) => a.localeCompare(b)), state.cpu_name);
            optionList('#filterRam', uniqueSorted(base.map(c => c.ram_total), 'ram_total'), state.ram_total);
            optionList('#filterException', [...new Set(base.map(c => c.crash_exception).filter(Boolean))].sort((a, b) => a.localeCompare(b)), state.crash_exception);
        }

        function renderFilters() {
            document.getElementById('filters').innerHTML = `
                <label class="field">Search
                    <input type="search" id="filterSearch" placeholder="Search logs, level, CPU…" value="${escapeHtml(state.search)}" />
                </label>
                <label class="field">App version
                    <select id="filterAppVersion"></select>
                </label>
                <label class="field">Game version
                    <select id="filterGameVersion"></select>
                </label>
                <label class="field">Exception
                    <select id="filterException"></select>
                </label>
                <label class="field">OS
                    <select id="filterOs"></select>
                </label>
                <label class="field">CPU
                    <select id="filterCpu"></select>
                </label>
                <label class="field">RAM
                    <select id="filterRam"></select>
                </label>
            `;

            rebuildFilterOptions();

            const bind = (id, key) => {
                document.getElementById(id).addEventListener('change', (e) => {
                    state[key] = e.target.value;
                    refresh();
                });
            };

            document.getElementById('filterSearch').addEventListener('input', (e) => {
                state.search = e.target.value.trim();
                refresh({ preserveFilterDom: true });
            });
            bind('filterAppVersion', 'application_version');
            bind('filterGameVersion', 'game_version');
            bind('filterException', 'crash_exception');
            bind('filterOs', 'os_name');
            bind('filterCpu', 'cpu_name');
            bind('filterRam', 'ram_total');
        }

        function setupTimelineValues(resetRange = false) {
            const dim = state.timelineDimension;
            const values = uniqueSorted(ALL_CRASHES.map(c => c[dim]), dim);
            state.timelineValues = values;
            if (resetRange || state.timelineMax >= values.length || state.timelineMin >= values.length) {
                state.timelineMin = 0;
                state.timelineMax = Math.max(0, values.length - 1);
            } else {
                state.timelineMin = Math.max(0, Math.min(state.timelineMin, values.length - 1));
                state.timelineMax = Math.max(state.timelineMin, Math.min(state.timelineMax, values.length - 1));
            }
            renderTimeline();
        }

        function timelinePercent(index) {
            const n = state.timelineValues.length;
            if (n <= 1) return 0;
            return (index / (n - 1)) * 100;
        }

        function renderTimeline() {
            const values = state.timelineValues;
            const fill = document.getElementById('timelineFill');
            const handleMin = document.getElementById('timelineHandleMin');
            const handleMax = document.getElementById('timelineHandleMax');
            const ticks = document.getElementById('timelineTicks');
            const label = document.getElementById('timelineRangeLabel');

            if (!values.length) {
                label.textContent = 'No values for this dimension';
                fill.style.left = '0%';
                fill.style.width = '0%';
                handleMin.style.left = '0%';
                handleMax.style.left = '0%';
                ticks.innerHTML = '';
                return;
            }

            const left = timelinePercent(state.timelineMin);
            const right = timelinePercent(state.timelineMax);
            fill.style.left = left + '%';
            fill.style.width = Math.max(0, right - left) + '%';
            handleMin.style.left = left + '%';
            handleMax.style.left = right + '%';

            const start = values[state.timelineMin];
            const end = values[state.timelineMax];
            const countInRange = values.slice(state.timelineMin, state.timelineMax + 1).length;
            label.innerHTML = state.timelineMin === 0 && state.timelineMax === values.length - 1
                ? `Showing <strong>all ${values.length}</strong> ${dimensionLabel(state.timelineDimension)} values`
                : `Showing <strong>${escapeHtml(start)}</strong> → <strong>${escapeHtml(end)}</strong> <span class="muted">(${countInRange} of ${values.length})</span>`;

            const maxLabels = Math.min(values.length, 8);
            const step = Math.max(1, Math.ceil((values.length - 1) / Math.max(1, maxLabels - 1)));
            const indexes = [];
            for (let i = 0; i < values.length; i += step) indexes.push(i);
            if (indexes[indexes.length - 1] !== values.length - 1) indexes.push(values.length - 1);

            ticks.innerHTML = indexes.map(i => `
                <div class="timeline-tick" style="position:absolute;left:${timelinePercent(i)}%;transform:translateX(-50%);">
                    <span class="mark"></span>
                    <span class="label" title="${escapeHtml(values[i])}">${escapeHtml(values[i])}</span>
                </div>
            `).join('');
        }

        function dimensionLabel(dim) {
            return ({
                application_version: 'version',
                os_name: 'OS',
                cpu_name: 'CPU',
                ram_total: 'RAM',
            })[dim] || dim;
        }

        function indexFromPointer(clientX) {
            const track = document.getElementById('timelineTrack');
            const rect = track.getBoundingClientRect();
            const pad = 8;
            const usable = Math.max(1, rect.width - pad * 2);
            const ratio = Math.min(1, Math.max(0, (clientX - rect.left - pad) / usable));
            const n = state.timelineValues.length;
            if (n <= 1) return 0;
            return Math.round(ratio * (n - 1));
        }

        function bindTimelineDrag() {
            let active = null;

            const onMove = (clientX) => {
                if (!active || !state.timelineValues.length) return;
                const idx = indexFromPointer(clientX);
                if (active === 'min') {
                    state.timelineMin = Math.min(idx, state.timelineMax);
                } else {
                    state.timelineMax = Math.max(idx, state.timelineMin);
                }
                renderTimeline();
                refresh({ preserveFilterDom: true, skipTimelineSetup: true });
            };

            const start = (which) => (e) => {
                active = which;
                e.preventDefault();
                const point = e.touches ? e.touches[0].clientX : e.clientX;
                onMove(point);
            };

            document.getElementById('timelineHandleMin').addEventListener('mousedown', start('min'));
            document.getElementById('timelineHandleMax').addEventListener('mousedown', start('max'));
            document.getElementById('timelineHandleMin').addEventListener('touchstart', start('min'), { passive: false });
            document.getElementById('timelineHandleMax').addEventListener('touchstart', start('max'), { passive: false });

            document.getElementById('timelineTrack').addEventListener('mousedown', (e) => {
                if (e.target.classList.contains('timeline-handle')) return;
                const idx = indexFromPointer(e.clientX);
                const distMin = Math.abs(idx - state.timelineMin);
                const distMax = Math.abs(idx - state.timelineMax);
                active = distMin <= distMax ? 'min' : 'max';
                onMove(e.clientX);
            });

            window.addEventListener('mousemove', (e) => onMove(e.clientX));
            window.addEventListener('touchmove', (e) => {
                if (!active) return;
                onMove(e.touches[0].clientX);
            }, { passive: true });
            window.addEventListener('mouseup', () => { active = null; });
            window.addEventListener('touchend', () => { active = null; });
        }

        function renderStats(filtered) {
            const uniqueTypes = new Set(filtered.map(c => c.crash_signature)).size;
            const uniqueVersions = new Set(filtered.map(c => c.application_version)).size;
            document.getElementById('stats').innerHTML = `
                <div class="stat"><div class="label">Showing</div><div class="value">${filtered.length}</div></div>
                <div class="stat"><div class="label">Total stored</div><div class="value">${ALL_CRASHES.length}</div></div>
                <div class="stat"><div class="label">Crash types</div><div class="value">${uniqueTypes}</div></div>
                <div class="stat"><div class="label">Versions</div><div class="value">${uniqueVersions}</div></div>
            `;
        }

        function renderVersionTable(filtered) {
            const map = new Map();
            filtered.forEach(c => {
                const key = c.application_version + '||' + c.game_version;
                if (!map.has(key)) {
                    map.set(key, {
                        application_version: c.application_version,
                        game_version: c.game_version,
                        count: 0,
                    });
                }
                map.get(key).count += 1;
            });

            const rows = [...map.values()].sort((a, b) => compareVersions(b.application_version, a.application_version));
            const body = document.getElementById('versionTableBody');
            if (!rows.length) {
                body.innerHTML = '<tr><td colspan="3" class="empty">No crashes in current filter</td></tr>';
                return;
            }

            body.innerHTML = rows.map(r => `
                <tr>
                    <td class="mono">${escapeHtml(r.application_version || '—')}</td>
                    <td>${escapeHtml(r.game_version || '—')}</td>
                    <td><span class="pill">${r.count}</span></td>
                </tr>
            `).join('');
        }

        function renderChart(filtered) {
            const byDay = new Map();
            filtered.forEach(c => {
                const day = String(c.datetime || '').slice(0, 10);
                if (!day) return;
                byDay.set(day, (byDay.get(day) || 0) + 1);
            });
            const labels = [...byDay.keys()].sort();
            const values = labels.map(l => byDay.get(l));

            const ctx = document.getElementById('crashesOverTimeChart');
            if (state.chart) state.chart.destroy();
            state.chart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels,
                    datasets: [{
                        label: 'Crashes / day',
                        data: values,
                        fill: true,
                        tension: 0.25,
                        borderColor: '#0b6bcb',
                        backgroundColor: 'rgba(11, 107, 203, 0.12)',
                        pointRadius: labels.length > 60 ? 0 : 3,
                        borderWidth: 2,
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        y: { beginAtZero: true, ticks: { precision: 0 } },
                        x: { ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } }
                    }
                }
            });
        }

        function renderGrouped(filtered) {
            const groups = new Map();
            filtered.forEach(c => {
                const key = c.crash_signature || 'Unknown';
                if (!groups.has(key)) {
                    groups.set(key, {
                        signature: key,
                        exception: c.crash_exception || 'Unknown',
                        message: c.crash_message || '',
                        handler: c.crash_handler || '',
                        crashes: [],
                    });
                }
                groups.get(key).crashes.push(c);
            });

            const ordered = [...groups.values()].sort((a, b) => b.crashes.length - a.crashes.length);
            const root = document.getElementById('groupedView');
            document.getElementById('listTitle').textContent = `Crashes by type (${ordered.length})`;

            if (!ordered.length) {
                root.innerHTML = '<div class="empty">No crashes match the current filters</div>';
                return;
            }

            root.innerHTML = ordered.map(g => {
                const latest = g.crashes[0];
                const shown = g.crashes.slice(0, 20);
                const handlerBit = g.handler ? escapeHtml(g.handler) + ' · ' : '';
                const message = g.message
                    ? escapeHtml(g.message)
                    : '<span class="muted">No message captured</span>';

                return `
                <details class="group">
                    <summary>
                        <span class="pill">${g.crashes.length}</span>
                        <div class="group-main">
                            <div class="group-exception">${escapeHtml(shortExceptionName(g.exception))}</div>
                            <div class="group-message">${message}</div>
                            <div class="group-meta">${handlerBit}latest ${escapeHtml(latest?.datetime || '—')} · ${escapeHtml(latest?.application_version || '—')}</div>
                        </div>
                        <span class="group-chevron" aria-hidden="true">›</span>
                    </summary>
                    <div class="group-body">
                        <table>
                            <thead>
                                <tr>
                                    <th>ID</th>
                                    <th>When</th>
                                    <th>Version</th>
                                    <th>System</th>
                                    <th>Context</th>
                                    <th></th>
                                </tr>
                            </thead>
                            <tbody>
                                ${shown.map(c => `
                                    <tr>
                                        <td class="mono">#${escapeHtml(c.id)}</td>
                                        <td>${escapeHtml(c.datetime || '')}</td>
                                        <td>${escapeHtml(c.application_version || '—')}<div class="muted">${escapeHtml(c.game_version || '')}</div></td>
                                        <td>${escapeHtml(c.os_name || '—')}<div class="muted">${escapeHtml(c.cpu_name || '—')} · ${escapeHtml(c.ram_total || '—')}</div></td>
                                        <td>${escapeHtml(c.current_level || '—')}<div class="muted">${escapeHtml(c.current_composite || '—')}</div></td>
                                        <td><button type="button" class="primary" data-log-id="${escapeHtml(c.id)}">View log</button></td>
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>
                        ${g.crashes.length > shown.length
                            ? `<div class="group-more">Showing ${shown.length} of ${g.crashes.length}. Narrow filters to see the rest.</div>`
                            : ''}
                    </div>
                </details>`;
            }).join('');
        }

        function shortExceptionName(name) {
            const raw = String(name || 'Unknown');
            const parts = raw.split('.');
            return parts[parts.length - 1] || raw;
        }

        function renderTable(filtered) {
            const body = document.getElementById('crashTableBody');
            document.getElementById('listTitle').textContent = `Crash table (${filtered.length})`;
            if (!filtered.length) {
                body.innerHTML = '<tr><td colspan="7" class="empty">No crashes match the current filters</td></tr>';
                return;
            }

            body.innerHTML = filtered.map(c => `
                <tr>
                    <td class="narrow mono">#${escapeHtml(c.id)}</td>
                    <td class="narrow">${escapeHtml(c.datetime || '')}<div class="muted">${escapeHtml(c.uptime || '')}</div></td>
                    <td class="wrap mono">${escapeHtml(c.crash_signature || '')}</td>
                    <td class="narrow">${escapeHtml(c.application_version || '')}<div class="muted">${escapeHtml(c.game_version || '')}</div></td>
                    <td class="wrap">${escapeHtml(c.os_name || '—')}<div class="muted">${escapeHtml(c.cpu_name || '—')} · ${escapeHtml(c.ram_total || '—')}</div></td>
                    <td class="wrap">${escapeHtml(c.current_level || '—')}<div class="muted">${escapeHtml(c.current_composite || '—')} / ${escapeHtml(c.current_entity || '—')}</div></td>
                    <td class="narrow"><button type="button" class="primary" data-log-id="${escapeHtml(c.id)}">View log</button></td>
                </tr>
            `).join('');
        }

        function setView(view) {
            state.view = view;
            document.getElementById('viewGrouped').classList.toggle('active', view === 'grouped');
            document.getElementById('viewTable').classList.toggle('active', view === 'table');
            document.getElementById('groupedView').style.display = view === 'grouped' ? 'flex' : 'none';
            document.getElementById('tableView').style.display = view === 'table' ? 'block' : 'none';
            const filtered = getFilteredCrashes();
            if (view === 'grouped') renderGrouped(filtered);
            else renderTable(filtered);
        }

        function refresh(options = {}) {
            if (!options.skipTimelineSetup) {
                // keep values, just re-render track
                renderTimeline();
            }
            if (!options.preserveFilterDom) {
                rebuildFilterOptions();
            } else {
                // still rebuild select options so counts stay coherent when search types,
                // but avoid wiping search input caret: rebuild only selects
                const searchEl = document.getElementById('filterSearch');
                const caret = searchEl ? searchEl.selectionStart : null;
                rebuildFilterOptions();
                if (searchEl && caret != null) {
                    searchEl.focus();
                    searchEl.setSelectionRange(caret, caret);
                }
            }

            const filtered = getFilteredCrashes();
            renderStats(filtered);
            renderVersionTable(filtered);
            renderChart(filtered);
            if (state.view === 'grouped') renderGrouped(filtered);
            else renderTable(filtered);
        }

        async function showErrorLog(id) {
            const modal = document.getElementById('errorModal');
            const content = document.getElementById('errorLogContent');
            document.getElementById('modalTitle').textContent = `Crash log #${id}`;
            content.textContent = 'Loading…';
            modal.classList.add('open');
            modal.setAttribute('aria-hidden', 'false');
            try {
                const response = await fetch(`get_log.php?id=${encodeURIComponent(id)}`);
                const data = await response.text();
                content.textContent = data || 'Log not found.';
            } catch (err) {
                content.textContent = 'Failed to load log.';
                console.error(err);
            }
        }

        function closeModal() {
            const modal = document.getElementById('errorModal');
            modal.classList.remove('open');
            modal.setAttribute('aria-hidden', 'true');
        }

        document.getElementById('closeModalBtn').addEventListener('click', closeModal);
        document.getElementById('errorModal').addEventListener('click', (e) => {
            if (e.target.id === 'errorModal') closeModal();
        });
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') closeModal();
        });

        document.getElementById('viewGrouped').addEventListener('click', () => setView('grouped'));
        document.getElementById('viewTable').addEventListener('click', () => setView('table'));

        document.getElementById('timelineDimension').addEventListener('change', (e) => {
            state.timelineDimension = e.target.value;
            setupTimelineValues(true);
            refresh({ skipTimelineSetup: true });
        });

        document.getElementById('resetTimeline').addEventListener('click', () => {
            setupTimelineValues(true);
            refresh({ skipTimelineSetup: true });
        });

        document.getElementById('resetFilters').addEventListener('click', () => {
            state.search = '';
            state.application_version = '';
            state.game_version = '';
            state.os_name = '';
            state.cpu_name = '';
            state.ram_total = '';
            state.crash_exception = '';
            document.getElementById('filterSearch').value = '';
            setupTimelineValues(true);
            refresh();
        });

        document.body.addEventListener('click', (e) => {
            const btn = e.target.closest('[data-log-id]');
            if (!btn) return;
            showErrorLog(btn.getAttribute('data-log-id'));
        });

        renderFilters();
        setupTimelineValues(true);
        bindTimelineDrag();
        refresh({ skipTimelineSetup: true });
    </script>
</body>
</html>
