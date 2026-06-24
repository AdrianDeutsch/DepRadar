"use strict";

const els = {
  input: document.getElementById("packageInput"),
  scan: document.getElementById("scanButton"),
  status: document.getElementById("status"),
  results: document.getElementById("results"),
  overall: document.getElementById("overall"),
  graph: document.getElementById("graph"),
  tableBody: document.querySelector("#riskTable tbody"),
  drill: document.getElementById("drill"),
  upgrade: document.getElementById("upgrade"),
  report: document.getElementById("reportButton"),
};

const LEVEL_COLOR = {
  None: "#34d399", Low: "#34d399", Medium: "#fbbf24", High: "#fb923c", Critical: "#f87171",
};

let currentPackage = null;
let riskRows = [];
let sortKey = "score";
let sortAsc = true;

// ---- SignalR live progress -------------------------------------------------

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/scan")
  .withAutomaticReconnect()
  .build();

connection.on("ScanUpdated", (scan) => {
  setStatus(scan.status, `${scan.packagesDiscovered} pkgs · ${scan.edgesWritten} edges`);
  if (scan.status === "Completed") {
    loadResults(scan.rootPackageId);
  } else if (scan.status === "Failed") {
    setStatus("Failed", scan.error || "scan failed", true);
  }
});

connection.start().catch((err) => console.error("SignalR connect failed", err));

// ---- Scan ------------------------------------------------------------------

els.scan.addEventListener("click", startScan);
els.input.addEventListener("keydown", (e) => { if (e.key === "Enter") startScan(); });

async function startScan() {
  const pkg = els.input.value.trim();
  if (!pkg) return;
  els.scan.disabled = true;
  els.results.classList.add("hidden");
  setStatus("Queued", "submitting…");

  try {
    const res = await fetch(`/api/packages/${encodeURIComponent(pkg)}/scan`, { method: "POST" });
    if (!res.ok) throw new Error(`scan request failed (${res.status})`);
    const scan = await res.json();
    await connection.invoke("Subscribe", scan.id);
    setStatus(scan.status, "waiting for the worker…");
  } catch (err) {
    setStatus("Failed", err.message, true);
  } finally {
    els.scan.disabled = false;
  }
}

function setStatus(state, detail, failed) {
  const cls = failed ? "pill failed" : "pill";
  els.status.innerHTML = `<span class="${cls}">${state}</span>${detail ? ` ${detail}` : ""}`;
}

// ---- Results ---------------------------------------------------------------

async function loadResults(pkg) {
  currentPackage = pkg;
  setStatus("Completed", "rendering report…");

  const [graph, risk, upgrade] = await Promise.all([
    getJson(`/api/packages/${encodeURIComponent(pkg)}/graph`),
    getJson(`/api/packages/${encodeURIComponent(pkg)}/graph/risk`),
    getJson(`/api/packages/${encodeURIComponent(pkg)}/upgrade`),
  ]);

  if (!risk) return;

  const levelByPkg = {};
  risk.packages.forEach((p) => { levelByPkg[p.packageId.toLowerCase()] = p.level; });

  els.overall.textContent = `health ${risk.overallScore}/100 (${risk.overallLevel}) · ${risk.packagesAssessed} packages`;
  renderGraph(graph, levelByPkg);
  riskRows = risk.packages;
  renderTable();
  renderUpgrade(upgrade);

  els.results.classList.remove("hidden");
  setStatus("Completed", `${risk.packagesAssessed} packages assessed`);
}

async function getJson(url) {
  const res = await fetch(url);
  return res.ok ? res.json() : null;
}

function renderGraph(graph, levelByPkg) {
  if (!graph) { els.graph.innerHTML = ""; return; }

  const nodes = graph.nodes.map((n) => ({
    data: {
      id: n.packageId,
      label: `${n.packageId}\n${n.version}`,
      color: LEVEL_COLOR[levelByPkg[n.packageId.toLowerCase()] || "None"],
      root: n.isRoot ? 1 : 0,
    },
  }));
  const edges = graph.edges.map((e) => ({ data: { source: e.fromId, target: e.toId } }));

  cytoscape({
    container: els.graph,
    elements: [...nodes, ...edges],
    style: [
      { selector: "node", style: {
          "background-color": "data(color)", "label": "data(label)", "color": "#e6edf3",
          "font-size": 9, "text-wrap": "wrap", "text-valign": "center", "text-halign": "center",
          "width": 26, "height": 26, "text-max-width": 90 } },
      { selector: "node[root = 1]", style: { "width": 40, "height": 40, "border-width": 3, "border-color": "#2dd4bf" } },
      { selector: "edge", style: {
          "width": 1.4, "line-color": "#2b3b57", "target-arrow-color": "#2b3b57",
          "target-arrow-shape": "triangle", "curve-style": "bezier", "arrow-scale": 0.8 } },
    ],
    layout: { name: "cose", animate: false, padding: 20 },
  });
}

function renderTable() {
  const rows = [...riskRows].sort((a, b) => {
    const x = a[sortKey], y = b[sortKey];
    const cmp = typeof x === "number" ? x - y : String(x).localeCompare(String(y));
    return sortAsc ? cmp : -cmp;
  });

  els.tableBody.innerHTML = rows.map((p, i) => `
    <tr data-index="${i}">
      <td>${escapeHtml(p.packageId)}</td>
      <td>${escapeHtml(p.version)}</td>
      <td class="num">${p.score}</td>
      <td><span class="badge ${p.level}">${p.level}</span></td>
    </tr>`).join("");

  els.tableBody.querySelectorAll("tr").forEach((tr) => {
    tr.addEventListener("click", () => showDrill(rows[Number(tr.dataset.index)]));
  });
}

function showDrill(pkg) {
  if (!pkg.findings.length) {
    els.drill.innerHTML = `<strong>${escapeHtml(pkg.packageId)} ${escapeHtml(pkg.version)}</strong> — no findings.`;
    return;
  }
  const items = pkg.findings.map((f) =>
    `<li><span class="badge ${f.level}">${f.level}</span> ${escapeHtml(f.category)}: ${escapeHtml(f.message)}</li>`).join("");
  els.drill.innerHTML = `<strong>${escapeHtml(pkg.packageId)} ${escapeHtml(pkg.version)}</strong><ul>${items}</ul>`;
}

function renderUpgrade(advice) {
  if (!advice) { els.upgrade.innerHTML = "<span class='narrative'>No upgrade advice available.</span>"; return; }
  const points = advice.keyPoints.map((p) => `<li>${escapeHtml(p)}</li>`).join("");
  els.upgrade.innerHTML = `
    <div class="rec">${escapeHtml(advice.fromVersion)} → ${escapeHtml(advice.toVersion)}: ${escapeHtml(advice.recommendation)}
      ${advice.llmUsed ? "" : "<span style='font-size:12px;color:#5b6b82'>(templated — set an LLM key for an AI narrative)</span>"}</div>
    <div class="narrative">${escapeHtml(advice.narrative)}</div>
    ${points ? `<ul>${points}</ul>` : ""}`;
}

document.querySelectorAll("#riskTable th[data-sort]").forEach((th) => {
  th.addEventListener("click", () => {
    const key = th.dataset.sort;
    sortAsc = sortKey === key ? !sortAsc : true;
    sortKey = key;
    renderTable();
  });
});

els.report.addEventListener("click", async () => {
  if (!currentPackage) return;
  const res = await fetch(`/api/packages/${encodeURIComponent(currentPackage)}/report`);
  if (!res.ok) return;
  const text = await res.text();
  const url = URL.createObjectURL(new Blob([text], { type: "text/markdown" }));
  const a = document.createElement("a");
  a.href = url;
  a.download = `${currentPackage}-depradar-report.md`;
  a.click();
  URL.revokeObjectURL(url);
});

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
