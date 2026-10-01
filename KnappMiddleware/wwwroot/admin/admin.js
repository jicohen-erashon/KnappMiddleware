/* KnappMiddleware · Admin panel client
 *
 * Sin dependencias. fetch + DOM nativo.
 *
 * Endpoints:
 *   GET    /api/v1/configurations                  (lista)
 *   GET    /api/v1/configurations/{clave}          (1 item)
 *   GET    /api/v1/configurations/{clave}/history  (cambios previos)
 *   POST   /api/v1/configurations                  (crear)
 *   PUT    /api/v1/configurations/{clave}          (actualizar)
 *   DELETE /api/v1/configurations/{clave}          (eliminar)
 *   POST   /api/v1/config/reload                   (recargar snapshot)
 *   GET    /api/v1/audit?direccion=&take=&correlationId=  (auditoría)
 *   GET    /api/v1/status                          (estado canales)
 *   POST   /api/v1/tcp/reconnect                   (reconectar)
 *   GET    /api/v1/matrix                          (matriz)
 *   POST   /api/v1/matrix/reload                   (recargar matriz)
 *   GET    /api/v1/users                           (lista, sin hash)
 *   POST   /api/v1/users                           (crear)
 *   PUT    /api/v1/users/{nombreUsuario}           (actualizar rol/habilitado)
 *   POST   /api/v1/users/{nombreUsuario}/reset-password (restablecer contraseña)
 *   DELETE /api/v1/users/{nombreUsuario}           (eliminar)
 *   GET    /api/v1/admin/auth/me                    (sesión actual)
 *   POST   /api/v1/admin/auth/logout                (cerrar sesión)
 *
 * Sesión vía cookie (login.html); cualquier 401 redirige ahí.
 */

'use strict';

// ---------- utils ----------
const $  = sel => document.querySelector(sel);
const $$ = sel => Array.from(document.querySelectorAll(sel));
const sleep = ms => new Promise(r => setTimeout(r, ms));

function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function fmtFecha(s) {
  if (!s) return '—';
  try { return new Date(s).toLocaleString('es-GT', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' }); }
  catch { return String(s); }
}

function fmtRel(s) {
  if (!s) return '';
  const ms = Date.now() - new Date(s).getTime();
  if (ms < 60_000) return 'hace segundos';
  if (ms < 3_600_000) return `hace ${Math.floor(ms / 60_000)} min`;
  if (ms < 86_400_000) return `hace ${Math.floor(ms / 3_600_000)} h`;
  return `hace ${Math.floor(ms / 86_400_000)} días`;
}

function metaCell(iso) {
  if (!iso) return '<span class="ts">—</span>';
  return `<span class="ts" title="${escapeHtml(fmtFecha(iso))}">${escapeHtml(fmtRel(iso))}</span>`;
}

function debounce(fn, ms = 180) {
  let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
}

function jsonHeaders() { return { 'Content-Type': 'application/json', 'Accept': 'application/json' }; }

async function apiCall(p, { allowEmpty = false } = {}) {
  const r = await p();
  if (r.status === 401) { window.location.href = 'login.html'; throw new Error('401'); }
  if (!r.ok && r.status !== 204) {
    let detail = `${r.status}`;
    try { const j = await r.json(); detail = j.error || j.detail || JSON.stringify(j); } catch {}
    const err = new Error(detail);
    err.status = r.status;
    throw err;
  }
  if (allowEmpty && r.status === 204) return null;
  return r.json();
}

const API = {
  cfgList:      ()  => fetch('/api/v1/configurations',                   { credentials: 'include' }),
  cfgGet:       k   => fetch(`/api/v1/configurations/${encodeURIComponent(k)}`, { credentials: 'include' }),
  cfgHistory:   (k, take = 50) => fetch(`/api/v1/configurations/${encodeURIComponent(k)}/history?take=${take}`, { credentials: 'include' }),
  cfgCreate:    b   => fetch('/api/v1/configurations',                   { method: 'POST',   credentials: 'include', headers: jsonHeaders(), body: JSON.stringify(b) }),
  cfgUpdate:    (k, b) => fetch(`/api/v1/configurations/${encodeURIComponent(k)}`, { method: 'PUT',    credentials: 'include', headers: jsonHeaders(), body: JSON.stringify(b) }),
  cfgDelete:    k   => fetch(`/api/v1/configurations/${encodeURIComponent(k)}`, { method: 'DELETE', credentials: 'include' }),
  cfgReload:    ()  => fetch('/api/v1/config/reload',                    { method: 'POST',   credentials: 'include' }),
  auditList:    (direccion, take, correlationId) => {
    const qs = new URLSearchParams({ direccion, take: String(take) });
    if (correlationId) qs.set('correlationId', correlationId);
    return fetch(`/api/v1/audit?${qs}`, { credentials: 'include' });
  },
  chanStatus:   ()  => fetch('/api/v1/status',                            { credentials: 'include' }),
  chanReconn:   ()  => fetch('/api/v1/tcp/reconnect',                     { method: 'POST',   credentials: 'include' }),
  matList:      ()  => fetch('/api/v1/matrix',                            { credentials: 'include' }),
  matReload:    ()  => fetch('/api/v1/matrix/reload',                     { method: 'POST',   credentials: 'include' }),
  userList:     ()  => fetch('/api/v1/users',                             { credentials: 'include' }),
  userCreate:   b   => fetch('/api/v1/users',                             { method: 'POST',   credentials: 'include', headers: jsonHeaders(), body: JSON.stringify(b) }),
  userUpdate:   (u, b) => fetch(`/api/v1/users/${encodeURIComponent(u)}`,  { method: 'PUT',    credentials: 'include', headers: jsonHeaders(), body: JSON.stringify(b) }),
  userResetPassword: (u, b) => fetch(`/api/v1/users/${encodeURIComponent(u)}/reset-password`, { method: 'POST', credentials: 'include', headers: jsonHeaders(), body: JSON.stringify(b) }),
  userDelete:   u   => fetch(`/api/v1/users/${encodeURIComponent(u)}`,     { method: 'DELETE', credentials: 'include' }),
  authMe:       ()  => fetch('/api/v1/admin/auth/me',                     { credentials: 'include' }),
  authLogout:   ()  => fetch('/api/v1/admin/auth/logout',                 { method: 'POST',   credentials: 'include' }),
};

// ---------- state ----------
const state = {
  configs: [],
  filter: { search: '', group: '' },
  dirty: new Map(),          // clave → { original, current }
  expanded: new Set(),      // prefijos de grupos expandidos
  audit: { dir: 'Entrada', take: 200, correlationId: '', search: '' },
  matrix: [],
  users: [],
  userFilter: '',
  theme: localStorage.getItem('km.theme') || 'light',
};

function applyTheme() {
  document.documentElement.dataset.theme = state.theme;
  $('#themeToggle').textContent = state.theme === 'light' ? '☾' : '☀';
  $('#themeToggle').title = state.theme === 'light' ? 'Cambiar a tema oscuro' : 'Cambiar a tema claro';
}

// ---------- toast stack ----------
const TOAST_INTENT = { ok: 'success', warn: 'warning', err: 'error' };
function toast(kind, title, desc, opts = {}) {
  const host = $('#toastHost');
  const el = document.createElement('fluent-message-bar');
  el.setAttribute('intent', TOAST_INTENT[kind] ?? 'info');
  el.className = 'toast';
  el.innerHTML = `
    <div class="body">
      <div class="title">${escapeHtml(title)}</div>
      ${desc ? `<div class="desc">${escapeHtml(desc)}</div>` : ''}
      ${opts.actions ? `<div class="actions">${opts.actions}</div>` : ''}
    </div>
    <fluent-button slot="dismiss" appearance="transparent" icon-only class="close" aria-label="Cerrar">×</fluent-button>`;
  host.appendChild(el);
  el.querySelector('.close').addEventListener('click', () => el.remove());
  if (opts.actions) opts.bind(el);
  // errors stay; others fade
  if (kind !== 'err' && !opts.persist) setTimeout(() => el.remove(), 4500);
  return el;
}

// ---------- modal & confirm ----------
// fluent-dialog no es un <dialog> nativo (no hay evento 'close'/returnValue confiables),
// así que llevamos el diálogo abierto a mano en vez de consultar el DOM para Escape.
let openDialog = null;
function showModal(dlg) { openDialog = dlg; dlg.show(); }
function closeModal(dlg) { dlg.hide(); if (openDialog === dlg) openDialog = null; }

function confirmDialog(title, bodyHtml, { danger = true, okLabel = 'Confirmar' } = {}) {
  return new Promise(resolve => {
    const dlg = $('#confirmModal');
    $('#confirmTitle').textContent = title;
    $('#confirmBody').innerHTML = bodyHtml;
    const ok = $('#confirmOk');
    ok.textContent = okLabel;
    ok.classList.toggle('danger', danger);
    showModal(dlg);
    const done = (v) => { closeModal(dlg); ok.onclick = null; $('#confirmCancel').onclick = null; $('#confirmClose').onclick = null; resolve(v); };
    ok.onclick = () => done(true);
    $('#confirmCancel').onclick = () => done(false);
    $('#confirmClose').onclick = () => done(false);
  });
}

// ---------- tab strip ----------
function setActiveTab(name) {
  $$('.tab').forEach(b => { const on = b.dataset.tab === name; b.classList.toggle('active', on); b.setAttribute('aria-selected', on); });
  $$('main > section').forEach(s => s.classList.toggle('active', s.id === `tab-${name}`));
  if (loaders[name]) loaders[name]();
}

$$('.tab').forEach(b => b.addEventListener('click', () => setActiveTab(b.dataset.tab)));

function setTabCount(name, value) {
  const el = document.querySelector(`.tab-count[data-count="${name}"]`);
  if (el) el.textContent = value;
}

// ---------- theme ----------
$('#themeToggle').addEventListener('click', () => {
  state.theme = state.theme === 'light' ? 'dark' : 'light';
  localStorage.setItem('km.theme', state.theme);
  applyTheme();
});

// ---------- keyboard shortcuts ----------
let searchFocused = false;
document.addEventListener('keydown', e => {
  // Shadow DOM re-dirige 'keydown' (composed) al host cuando el listener vive fuera del shadow
  // root: e.target ya no es el <input> nativo sino el propio <fluent-text-input>, etc.
  if (e.target.matches('input, textarea, select, fluent-text-input, fluent-text-area, fluent-dropdown, fluent-checkbox')) return;
  if (e.key === '/') { e.preventDefault(); $('#cfgSearch').focus(); }
  else if (e.key === 'n') { e.preventDefault(); openEditModal(null); }
  else if (e.key === 't') { e.preventDefault(); $('#themeToggle').click(); }
  else if (e.key === 'Escape') {
    if (openDialog) closeModal(openDialog);
    else if (drawerOpen) closeDrawer();
  }
});

// ============================================================
// Configuraciones
// ============================================================

const loaders = {
  cfg:   loadConfigs,
  users: loadUsers,
  audit: loadAudit,
  chan:  loadChannels,
  mat:   loadMatrix,
};

// client-side mirror de UserGuardrails (Admin/UserGuardrails.cs)
const USER_GUARD = {
  nombreRe: /^[a-zA-Z][a-zA-Z0-9._\-]{2,63}$/,
  minContrasena: 8,
  maxContrasena: 128,
  validateNombreUsuario(n) {
    if (!n) return 'El nombre de usuario no puede estar vacío.';
    if (n.length > 64) return 'El nombre de usuario excede el máximo de 64 caracteres.';
    if (!this.nombreRe.test(n)) return 'Debe iniciar con letra y tener al menos 3 caracteres (letras/números/._-).';
    return null;
  },
  validateContrasena(p) {
    if (!p) return 'La contraseña no puede estar vacía.';
    if (p.length < this.minContrasena) return `La contraseña debe tener al menos ${this.minContrasena} caracteres.`;
    if (p.length > this.maxContrasena) return `La contraseña excede el máximo de ${this.maxContrasena} caracteres.`;
    return null;
  },
};

// client-side mirrors of AdminGuardrails
const GUARD = {
  claveRe: /^[a-zA-Z][a-zA-Z0-9._\-]{0,63}$/,
  typed: [
    { prefix: 'audit.enabled',       type: 'bool' },
    { prefix: 'audit.queueCapacity', type: 'int'  },
    { prefix: 'kiSoft.',             type: 'uri'  },
    { prefix: 'sap.webhook.baseUrl', type: 'uri'  },
    { prefix: 'sftp.',               type: 'uri'  },
    { prefix: 'rabbitmq.',           type: 'uri'  },
  ],
  validateClave(c) {
    if (!c) return 'La clave no puede estar vacía.';
    if (c.length > 64) return `La clave excede el máximo de 64 caracteres.`;
    if (!this.claveRe.test(c)) return 'Debe iniciar con letra y luego letras/números/._-.';
    return null;
  },
  validateValor(c, v) {
    if (v === null || v === undefined) return 'El valor no puede ser null. Use cadena vacía.';
    if (v.length > 8192) return `El valor excede el máximo de 8192 caracteres.`;
    for (const t of this.typed) {
      if (c.startsWith(t.prefix) && v.length > (t.type === 'bool' ? 5 : t.type === 'int' ? 10 : 512))
        return `El valor excede el máximo (${t.type === 'bool' ? 5 : t.type === 'int' ? 10 : 512}) caracteres para '${c}'.`;
      if (c === t.prefix && t.type !== 'any') {
        if (t.type === 'bool' && !/^(true|false)$/.test(v)) return `Para ${c} se esperaba 'true' o 'false'.`;
        if (t.type === 'int'  && !/^-?\d+$/.test(v))         return `Para ${c} se esperaba un entero.`;
      }
    }
    return null;
  },
};

function prefixOf(clave) {
  const i = clave.indexOf('.');
  return i === -1 ? '(sin grupo)' : clave.slice(0, i + 1);
}

function groupByPrefix(rows) {
  const m = new Map();
  for (const r of rows) {
    const p = prefixOf(r.clave);
    if (!m.has(p)) m.set(p, []);
    m.get(p).push(r);
  }
  for (const arr of m.values()) arr.sort((a, b) => a.clave.localeCompare(b.clave));
  return [...m.entries()].sort(([a], [b]) => a.localeCompare(b));
}

function isDirty(c) {
  const d = state.dirty.get(c.clave);
  return !!d && d.current !== d.original;
}

function dirtyCount() { return [...state.dirty.values()].filter(d => d.current !== d.original).length; }

function syncTabDirtyBadge() {
  const n = dirtyCount();
  const tab = $('.tab[data-tab="cfg"]');
  tab.classList.toggle('dirty', n > 0);
  const badge = $('#cfgDirtyBadge');
  if (n === 0) { badge.classList.add('hidden'); }
  else {
    badge.classList.remove('hidden');
    badge.textContent = `${n} cambio${n === 1 ? '' : 's'} pendiente${n === 1 ? '' : 's'}`;
  }
}

async function loadConfigs() {
  const host = $('#cfgGroups');
  host.innerHTML = skeletonRows();
  try {
    const rows = await apiCall(API.cfgList);
    state.configs = rows;
    // init expanded: todos los prefijos que tengan > 0 items, primero los abiertos
    if (state.expanded.size === 0) {
      for (const [p, items] of groupByPrefix(rows)) {
        if (['audit.', 'kiSoft.', 'sap.', 'sftp.'].includes(p) || items.length <= 5) state.expanded.add(p);
      }
    }
    renderConfigs();
    setTabCount('cfg', rows.length);
  } catch (e) {
    host.innerHTML = emptyState('No se pudo cargar la tabla configuración', e.message, [{ label: 'Reintentar', primary: true, action: loadConfigs }]);
    toast('err', 'Error al listar configuraciones', e.message, { persist: true });
  }
}

function skeletonRows() {
  const n = 6;
  let html = '<div class="group"><div class="group-header"><span class="group-caret"></span><span class="group-title skeleton" style="width:180px"></span></div><div class="group-body">';
  for (let i = 0; i < n; i++) {
    html += '<div class="skeleton-row"><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div></div>';
  }
  html += '</div></div>';
  return html;
}

function emptyState(title, desc, actions = []) {
  const acts = actions.map(a => `<fluent-button appearance="${a.primary ? 'primary' : 'outline'}" size="small" data-action="${a.label}">${escapeHtml(a.label)}</fluent-button>`).join(' ');
  return `<div class="empty"><h3>${escapeHtml(title)}</h3><p>${escapeHtml(desc)}</p>${actions.length ? `<div>${acts}</div>` : ''}</div>`;
}

function applyFilter(rows) {
  const s = state.filter.search.toLowerCase().trim();
  const g = state.filter.group;
  return rows.filter(r => {
    if (g === '__noGroup' && prefixOf(r.clave) !== '(sin grupo)') return false;
    if (g && g !== '__noGroup' && !r.clave.toLowerCase().startsWith(g.toLowerCase())) return false;
    if (!s) return true;
    return r.clave.toLowerCase().includes(s) || (r.valor ?? '').toLowerCase().includes(s) || (r.descripcion ?? '').toLowerCase().includes(s);
  });
}

function renderConfigs() {
  const host = $('#cfgGroups');
  const filtered = applyFilter(state.configs);
  if (filtered.length === 0) {
    host.innerHTML = emptyState('Sin configuraciones',
      state.configs.length === 0 ? 'La tabla `configuracion` está vacía.' : 'Ningún resultado coincide con el filtro actual.',
      state.configs.length === 0 ? [{ label: '+ Nueva', primary: true, action: () => openEditModal(null) }] : []);
    $('#cfgCount').textContent = `0 de ${state.configs.length}`;
    syncTabDirtyBadge();
    return;
  }
  const groups = groupByPrefix(filtered);
  host.innerHTML = groups.map(([prefix, items]) => {
    const expanded = state.expanded.has(prefix);
    const dirtyInGroup = items.some(isDirty);
    return `
      <section class="group ${expanded ? '' : 'collapsed'} ${dirtyInGroup ? 'has-dirty' : ''}" data-prefix="${escapeHtml(prefix)}">
        <div class="group-header" role="button" tabindex="0">
          <span class="group-caret"></span>
          <span class="group-title">${escapeHtml(prefix)}</span>
          <span class="group-count">${items.length}</span>
          <span class="group-dirty-badge">cambios pendientes</span>
        </div>
        <div class="group-body">
          ${items.map(renderRow).join('')}
        </div>
      </section>`;
  }).join('');
  bindRowEvents(host);
  $('#cfgCount').textContent = `${filtered.length} de ${state.configs.length}${state.filter.search ? ` (filtrado)` : ''}`;
  syncTabDirtyBadge();
}

function renderRow(r) {
  const dirty = isDirty(r);
  const currentVal = dirty ? state.dirty.get(r.clave).current : (r.valor ?? '');
  const cls = dirty ? 'row dirty' : 'row';
  return `
    <div class="${cls}" data-clave="${escapeHtml(r.clave)}">
      <div class="clave">${escapeHtml(r.clave)}</div>
      <div class="valor-wrap">
        <fluent-text-input class="valor ${dirty ? 'dirty' : ''}" value="${escapeHtml(currentVal)}" data-original="${escapeHtml(r.valor ?? '')}" spellcheck="false"></fluent-text-input>
        <div class="descripcion">${escapeHtml(r.descripcion ?? '')}</div>
      </div>
      <div class="meta">${metaCell(r.actualizadoEn)}</div>
      <div class="meta">creado ${metaCell(r.creadoEn)}</div>
      <div class="actions">
        <fluent-button appearance="outline" size="small" icon-only data-act="hist" title="Ver historial">↺</fluent-button>
        <fluent-button appearance="outline" size="small" icon-only data-act="reload" title="Recargar valor original">↻</fluent-button>
        <fluent-button appearance="primary" size="small" data-act="save" ${dirty ? '' : 'disabled'} title="Guardar (Ctrl+S)">Guardar</fluent-button>
      </div>
    </div>`;
}

function bindRowEvents(host) {
  host.querySelectorAll('.group-header').forEach(h => {
    const toggle = () => {
      const g = h.closest('.group');
      const p = g.dataset.prefix;
      g.classList.toggle('collapsed');
      if (g.classList.contains('collapsed')) state.expanded.delete(p); else state.expanded.add(p);
    };
    h.addEventListener('click', toggle);
    h.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); } });
  });
  host.querySelectorAll('.row').forEach(tr => {
    const clave = tr.dataset.clave;
    const input = tr.querySelector('fluent-text-input.valor');
    input.addEventListener('input', () => {
      const original = input.dataset.original;
      const current = input.value;
      if (current === original) state.dirty.delete(clave);
      else state.dirty.set(clave, { original, current });
      input.classList.toggle('dirty', current !== original);
      const save = tr.querySelector('[data-act="save"]');
      save.disabled = current === original;
      syncTabDirtyBadge();
    });
    input.addEventListener('keydown', e => { if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); tr.querySelector('[data-act="save"]').click(); } });
    tr.querySelector('[data-act="save"]').addEventListener('click', () => saveRow(clave));
    tr.querySelector('[data-act="reload"]').addEventListener('click', () => reloadRow(clave));
    tr.querySelector('[data-act="hist"]').addEventListener('click', () => openDrawer(clave));
  });
}

async function saveRow(clave) {
  const d = state.dirty.get(clave);
  if (!d) return;
  const original = state.configs.find(r => r.clave === clave);
  const ok = await confirmDialog(
    `Confirmar cambio: ${clave}`,
    `<p>Valor anterior:</p><pre style="background:var(--bg);padding:8px;border-radius:4px;border:1px solid var(--line);font-family:var(--mono);font-size:12px">${escapeHtml(d.original || '(vacío)')}</pre>
     <p style="margin-top:10px">Valor nuevo:</p><pre style="background:var(--bg);padding:8px;border-radius:4px;border:1px solid var(--line);font-family:var(--mono);font-size:12px;color:var(--ok)">${escapeHtml(d.current)}</pre>`,
    { danger: false, okLabel: 'Aplicar cambio' });
  if (!ok) return;
  try {
    const result = await apiCall(() => API.cfgUpdate(clave, { valor: d.current, descripcion: original?.descripcion ?? null }));
    toast('ok', `${clave} actualizado`, result.created ? 'No existía: se creó.' : 'Snapshot recargado en caliente.');
    state.dirty.delete(clave);
    await loadConfigs();
  } catch (e) { toast('err', `No se pudo guardar ${clave}`, e.message, { persist: true }); }
}

async function reloadRow(clave) {
  if (isDirty(state.configs.find(r => r.clave === clave))) {
    const ok = await confirmDialog('Descartar cambios', `<p>Vas a descartar el cambio pendiente en <code>${escapeHtml(clave)}</code> y recargar el valor original desde la BD.</p>`, { danger: true, okLabel: 'Descartar y recargar' });
    if (!ok) return;
  }
  try {
    const fresh = await apiCall(() => API.cfgGet(clave));
    state.dirty.delete(clave);
    const idx = state.configs.findIndex(r => r.clave === clave);
    if (idx >= 0) state.configs[idx] = fresh;
    renderConfigs();
    toast('ok', 'Recargado', `${clave} traído desde BD.`);
  } catch (e) { toast('err', 'No se pudo recargar', e.message, { persist: true }); }
}

// ----- filtro / toolbar -----
$('#cfgSearch').addEventListener('input', debounce(e => { state.filter.search = e.target.value; renderConfigs(); }));
$('#cfgGroup').addEventListener('change', e => { state.filter.group = e.target.value; renderConfigs(); });
$('#btnReloadAll').addEventListener('click', async () => {
  try { await apiCall(API.cfgReload, { allowEmpty: false }); toast('ok', 'Snapshot recargado', 'Todos los gates han releído la tabla.'); loadConfigs(); }
  catch (e) { toast('err', 'No se pudo recargar', e.message, { persist: true }); }
});
$('#btnExpand').addEventListener('click', () => {
  const groups = groupByPrefix(state.configs);
  const allExpanded = groups.every(([p]) => state.expanded.has(p));
  state.expanded.clear();
  if (!allExpanded) for (const [p] of groups) state.expanded.add(p);
  renderConfigs();
});

$('#btnNew').addEventListener('click', () => openEditModal(null));

// ----- modal crear/editar -----
function openEditModal(clave) {
  const isNew = clave === null;
  const original = !isNew ? state.configs.find(r => r.clave === clave) : null;
  $('#editTitle').textContent = isNew ? 'Nueva configuración' : `Editar: ${clave}`;
  $('#editClave').value = isNew ? '' : clave;
  $('#editClave').disabled = !isNew;
  $('#editClaveHint').textContent = 'Patrón: letra inicial + letras/números/._- (máx 64).';
  $('#editValor').value = isNew ? '' : (original?.valor ?? '');
  $('#editDescripcion').value = isNew ? '' : (original?.descripcion ?? '');
  $('#editClaveField').classList.remove('error');
  $('#editValorField').classList.remove('error');
  $('#editValorHint').textContent = 'Para audit.enabled use "true"/"false". Para audit.queueCapacity un entero. Para kiSoft.*/sap.*/sftp.* URL.';
  showModal($('#editModal'));
  setTimeout(() => $(isNew ? '#editClave' : '#editValor').focus(), 50);
}

$('#editClose').addEventListener('click', () => closeModal($('#editModal')));
$('#editCancel').addEventListener('click', () => closeModal($('#editModal')));
$('#editForm').addEventListener('submit', async e => {
  e.preventDefault();
  const isNew = !$('#editClave').disabled;
  const clave = $('#editClave').value.trim();
  const valor = $('#editValor').value;
  const descripcion = $('#editDescripcion').value.trim() || null;

  let ok = true;
  const ck = GUARD.validateClave(clave);
  if (ck) { $('#editClaveField').classList.add('error'); $('#editClaveError').textContent = ck; ok = false; }
  else { $('#editClaveField').classList.remove('error'); $('#editClaveError').textContent = ''; }
  const cv = GUARD.validateValor(clave, valor);
  if (cv) { $('#editValorField').classList.add('error'); $('#editValorError').textContent = cv; ok = false; }
  else { $('#editValorField').classList.remove('error'); $('#editValorError').textContent = ''; }
  if (!ok) return;

  try {
    if (isNew) {
      await apiCall(() => API.cfgCreate({ clave, valor, descripcion }));
      toast('ok', 'Creado', `${clave} dado de alta.`);
    } else {
      await apiCall(() => API.cfgUpdate(clave, { valor, descripcion }));
      toast('ok', 'Actualizado', `${clave} actualizado.`);
    }
    closeModal($('#editModal'));
    await loadConfigs();
  } catch (e) { toast('err', 'No se pudo guardar', e.message, { persist: true }); }
});

$('#editClave').addEventListener('input', () => $('#editClaveField').classList.remove('error'));
$('#editValor').addEventListener('input', () => $('#editValorField').classList.remove('error'));

// ============================================================
// Drawer historial
// ============================================================
let drawerOpen = false;
function openDrawer(clave) {
  $('#drawerClave').textContent = clave;
  $('#drawerBody').innerHTML = '<div class="skeleton" style="height:80px"></div>';
  $('#drawer').show();
  drawerOpen = true;
  loadHistory(clave);
}
function closeDrawer() {
  $('#drawer').hide();
  drawerOpen = false;
}
$('#drawerClose').addEventListener('click', closeDrawer);

async function loadHistory(clave) {
  try {
    const rows = await apiCall(() => API.cfgHistory(clave, 50));
    renderHistory(clave, rows);
  } catch (e) {
    $('#drawerBody').innerHTML = emptyState('No se pudo cargar el historial', e.message);
  }
}

function renderHistory(clave, rows) {
  if (!rows.length) {
    $('#drawerBody').innerHTML = emptyState('Sin cambios registrados', `Aún no hay entradas de auditoría para <code>${escapeHtml(clave)}</code>.`);
    return;
  }
  const html = ['<div class="timeline">'];
  for (const r of rows) {
    const oldVal = parseOld(r.payload);
    const newVal = parseNew(r.payload);
    html.push(`
      <div class="timeline-item">
        <div class="ti-head">
          <span><fluent-badge appearance="tint" color="brand">${escapeHtml(r.tipoTelegrama ?? r.tiporegistro ?? '?')}</fluent-badge> <span class="ti-meta">${fmtFecha(r.creadoEn)} · ${fmtRel(r.creadoEn)}</span></span>
          <fluent-badge appearance="tint" color="${badgeForEstado(r.estado)}">${escapeHtml(String(r.estado))}</fluent-badge>
        </div>
        <div class="ti-meta">${r.usuario ? `por <code>${escapeHtml(r.usuario)}</code>` : ''}</div>
        ${oldVal !== undefined || newVal !== undefined ? `
          <div style="margin-top:6px">${oldVal !== undefined ? `<div class="diff-line del"><span>−</span><span>${escapeHtml(oldVal ?? '(vacío)')}</span></div>` : ''}${newVal !== undefined ? `<div class="diff-line add"><span>+</span><span>${escapeHtml(newVal)}</span></div>` : ''}</div>
        ` : ''}
        ${r.errorDetalle ? `<div class="ti-meta" style="color:var(--err)">${escapeHtml(r.errorDetalle)}</div>` : ''}
        ${r.correlationId ? `<div class="ti-meta">correlation: <code>${escapeHtml(r.correlationId)}</code></div>` : ''}
      </div>`);
  }
  html.push('</div>');
  $('#drawerBody').innerHTML = html.join('');
}

function parseOld(payload) { try { const p = typeof payload === 'string' ? JSON.parse(payload) : payload; return p?.old; } catch { return undefined; } }
function parseNew(payload) { try { const p = typeof payload === 'string' ? JSON.parse(payload) : payload; return p?.new; } catch { return undefined; } }
// color de fluent-badge (BadgeColor: brand/danger/important/informative/severe/subtle/success/warning)
function badgeForEstado(e) { const v = String(e ?? '').toLowerCase(); if (v === '00') return 'success'; if (v === '99') return 'danger'; if (v === '') return 'subtle'; return 'warning'; }

// ============================================================
// Usuarios
// ============================================================
function badgeForBool(v, textTrue, textFalse) {
  return `<fluent-badge appearance="tint" color="${v ? 'success' : 'subtle'}">${v ? textTrue : textFalse}</fluent-badge>`;
}

async function loadUsers() {
  const host = $('#userBody');
  host.innerHTML = `<tr><td colspan="6" class="meta" style="text-align:center;padding:30px">Cargando…</td></tr>`;
  try {
    const rows = await apiCall(API.userList);
    state.users = rows;
    renderUsers();
    setTabCount('users', rows.length);
  } catch (e) {
    host.innerHTML = `<tr><td colspan="6" class="meta" style="text-align:center;padding:30px;color:var(--err)">${escapeHtml(e.message)}</td></tr>`;
    toast('err', 'Error al listar usuarios', e.message, { persist: true });
  }
}

function renderUsers() {
  const filter = state.userFilter.toLowerCase().trim();
  const rows = filter ? state.users.filter(u => u.nombreUsuario.toLowerCase().includes(filter)) : state.users;
  $('#userCount').textContent = `${rows.length} de ${state.users.length}${filter ? ' (filtrado)' : ''}`;
  if (!rows.length) {
    $('#userBody').innerHTML = `<tr><td colspan="6" class="meta" style="text-align:center;padding:30px">Sin usuarios.</td></tr>`;
    return;
  }
  $('#userBody').innerHTML = rows.map(u => `
    <tr>
      <td><code>${escapeHtml(u.nombreUsuario)}</code></td>
      <td><fluent-badge appearance="tint" color="brand">${escapeHtml(u.rol)}</fluent-badge></td>
      <td>${badgeForBool(u.habilitado, 'Sí', 'No')}</td>
      <td class="meta">${metaCell(u.creadoEn)}</td>
      <td class="meta">${metaCell(u.actualizadoEn)}</td>
      <td class="actions-cell">
        <fluent-button appearance="outline" size="small" data-uact="edit" data-u="${escapeHtml(u.nombreUsuario)}">Editar</fluent-button>
        <fluent-button appearance="outline" size="small" data-uact="pwd" data-u="${escapeHtml(u.nombreUsuario)}">Contraseña</fluent-button>
        <fluent-button appearance="primary" size="small" class="danger" data-uact="del" data-u="${escapeHtml(u.nombreUsuario)}">Eliminar</fluent-button>
      </td>
    </tr>`).join('');

  $('#userBody').querySelectorAll('[data-uact]').forEach(btn => {
    const u = btn.dataset.u;
    const act = btn.dataset.uact;
    btn.addEventListener('click', () => {
      if (act === 'edit') openUserModal(u);
      else if (act === 'pwd') openResetPasswordModal(u);
      else if (act === 'del') deleteUser(u);
    });
  });
}

$('#userSearch').addEventListener('input', debounce(e => { state.userFilter = e.target.value; renderUsers(); }));
$('#btnUserReload').addEventListener('click', loadUsers);
$('#btnUserNew').addEventListener('click', () => openUserModal(null));

function openUserModal(nombreUsuario) {
  const isNew = nombreUsuario === null;
  const existing = !isNew ? state.users.find(u => u.nombreUsuario === nombreUsuario) : null;
  $('#userModalTitle').textContent = isNew ? 'Nuevo usuario' : `Editar: ${nombreUsuario}`;
  $('#userNombre').value = isNew ? '' : nombreUsuario;
  $('#userNombre').disabled = !isNew;
  $('#userPassword').value = '';
  $('#userPasswordField').style.display = isNew ? '' : 'none';
  $('#userHabilitadoField').style.display = isNew ? 'none' : '';
  $('#userRol').value = isNew ? 'SuperUsuario' : existing.rol;
  $('#userHabilitado').checked = isNew ? true : existing.habilitado;
  $('#userNombreField').classList.remove('error');
  $('#userPasswordField').classList.remove('error');
  $('#userFormError').textContent = '';
  showModal($('#userModal'));
  setTimeout(() => $(isNew ? '#userNombre' : '#userRol').focus(), 50);
}

$('#userModalClose').addEventListener('click', () => closeModal($('#userModal')));
$('#userCancel').addEventListener('click', () => closeModal($('#userModal')));
$('#userNombre').addEventListener('input', () => $('#userNombreField').classList.remove('error'));
$('#userPassword').addEventListener('input', () => $('#userPasswordField').classList.remove('error'));

$('#userForm').addEventListener('submit', async e => {
  e.preventDefault();
  const isNew = !$('#userNombre').disabled;
  const nombreUsuario = $('#userNombre').value.trim();
  const rol = $('#userRol').value;
  const habilitado = $('#userHabilitado').checked;
  $('#userFormError').textContent = '';

  if (isNew) {
    const password = $('#userPassword').value;
    let ok = true;
    const nErr = USER_GUARD.validateNombreUsuario(nombreUsuario);
    if (nErr) { $('#userNombreField').classList.add('error'); $('#userNombreError').textContent = nErr; ok = false; }
    else { $('#userNombreField').classList.remove('error'); $('#userNombreError').textContent = ''; }
    const pErr = USER_GUARD.validateContrasena(password);
    if (pErr) { $('#userPasswordField').classList.add('error'); $('#userPasswordError').textContent = pErr; ok = false; }
    else { $('#userPasswordField').classList.remove('error'); $('#userPasswordError').textContent = ''; }
    if (!ok) return;

    try {
      await apiCall(() => API.userCreate({ nombreUsuario, contrasena: password, rol }));
      toast('ok', 'Usuario creado', `${nombreUsuario} dado de alta.`);
      closeModal($('#userModal'));
      await loadUsers();
    } catch (e) { $('#userFormError').textContent = e.message; toast('err', 'No se pudo crear el usuario', e.message, { persist: true }); }
  } else {
    try {
      await apiCall(() => API.userUpdate(nombreUsuario, { rol, habilitado }));
      toast('ok', 'Usuario actualizado', `${nombreUsuario} actualizado.`);
      closeModal($('#userModal'));
      await loadUsers();
    } catch (e) { $('#userFormError').textContent = e.message; toast('err', 'No se pudo actualizar el usuario', e.message, { persist: true }); }
  }
});

function openResetPasswordModal(nombreUsuario) {
  $('#resetPasswordUser').textContent = nombreUsuario;
  $('#resetPasswordValue').value = '';
  $('#resetPasswordField').classList.remove('error');
  $('#resetPasswordError').textContent = '';
  showModal($('#resetPasswordModal'));
  setTimeout(() => $('#resetPasswordValue').focus(), 50);
}

$('#resetPasswordClose').addEventListener('click', () => closeModal($('#resetPasswordModal')));
$('#resetPasswordCancel').addEventListener('click', () => closeModal($('#resetPasswordModal')));
$('#resetPasswordValue').addEventListener('input', () => $('#resetPasswordField').classList.remove('error'));

$('#resetPasswordForm').addEventListener('submit', async e => {
  e.preventDefault();
  const nombreUsuario = $('#resetPasswordUser').textContent;
  const contrasenaNueva = $('#resetPasswordValue').value;
  const pErr = USER_GUARD.validateContrasena(contrasenaNueva);
  if (pErr) { $('#resetPasswordField').classList.add('error'); $('#resetPasswordError').textContent = pErr; return; }

  try {
    await apiCall(() => API.userResetPassword(nombreUsuario, { contrasenaNueva }));
    toast('ok', 'Contraseña restablecida', `${nombreUsuario} tiene una nueva contraseña.`);
    closeModal($('#resetPasswordModal'));
  } catch (e) { $('#resetPasswordError').textContent = e.message; toast('err', 'No se pudo restablecer la contraseña', e.message, { persist: true }); }
});

async function deleteUser(nombreUsuario) {
  const ok = await confirmDialog(
    `Eliminar usuario: ${nombreUsuario}`,
    `<p>Esta acción es irreversible. <code>${escapeHtml(nombreUsuario)}</code> perderá acceso de inmediato.</p>`,
    { danger: true, okLabel: 'Eliminar' });
  if (!ok) return;
  try {
    await apiCall(() => API.userDelete(nombreUsuario), { allowEmpty: true });
    toast('ok', 'Usuario eliminado', `${nombreUsuario} fue eliminado.`);
    await loadUsers();
  } catch (e) { toast('err', 'No se pudo eliminar el usuario', e.message, { persist: true }); }
}

// ============================================================
// Auditoría
// ============================================================
async function loadAudit() {
  $$('#auditDir').length === 0;
  const host = $('#auditBody');
  host.innerHTML = '<tr><td colspan="5" class="meta" style="text-align:center;padding:30px">Cargando…</td></tr>';
  try {
    const rows = await apiCall(() => API.auditList(state.audit.dir, state.audit.take, state.audit.correlationId || null));
    state.auditRows = rows;
    renderAudit();
    setTabCount('audit', rows.length);
  } catch (e) {
    host.innerHTML = `<tr><td colspan="5" class="meta" style="text-align:center;padding:30px;color:var(--err)">${escapeHtml(e.message)}</td></tr>`;
    toast('err', 'Error al consultar auditoría', e.message, { persist: true });
  }
}

function renderAudit() {
  const rows = state.auditRows ?? [];
  const filter = state.audit.search.toLowerCase().trim();
  const filtered = filter ? rows.filter(r => JSON.stringify(r).toLowerCase().includes(filter)) : rows;
  $('#auditCount').textContent = `${filtered.length} de ${rows.length}${filter ? ' (filtrado)' : ''}`;
  if (!filtered.length) {
    $('#auditBody').innerHTML = `<tr><td colspan="5" class="meta" style="text-align:center;padding:30px">Sin entradas.</td></tr>`;
    return;
  }
  $('#auditBody').innerHTML = filtered.map(r => `
    <tr>
      <td class="meta">${metaCell(r.creadoEn)}</td>
      <td><code>${escapeHtml(r.tipoTelegrama ?? '?')}</code></td>
      <td><code class="meta">${escapeHtml(r.correlationId ?? '—')}</code></td>
      <td><pre class="payload">${escapeHtml(truncate(r.payload ?? '', 240))}</pre></td>
      <td><fluent-badge appearance="tint" color="${badgeForEstado(r.estado)}">${escapeHtml(String(r.estado))}</fluent-badge>${r.errorDetalle ? `<div class="meta" style="color:var(--err)">${escapeHtml(r.errorDetalle)}</div>` : ''}</td>
    </tr>`).join('');
}

function truncate(s, n) { s = s ?? ''; return s.length > n ? s.slice(0, n) + '…' : s; }

$('#auditSearch').addEventListener('input', debounce(e => { state.audit.search = e.target.value; renderAudit(); }));
$('#auditDir').addEventListener('change', e => { state.audit.dir = e.target.value; loadAudit(); });
$('#auditTake').addEventListener('change', e => { state.audit.take = parseInt(e.target.value || '200', 10); loadAudit(); });
$('#btnAuditReload').addEventListener('click', loadAudit);

// ============================================================
// Canales
// ============================================================
async function loadChannels() {
  $('#chanLast').textContent = 'Actualizado: ' + fmtFecha(new Date().toISOString());
  try {
    const s = await apiCall(API.chanStatus);
    state.chan = s;
    renderChannels(s);
    setTabCount('chan', 2); // pedido + evento
  } catch (e) {
    $('#chanGrid').innerHTML = emptyState('No se pudo consultar el estado', e.message);
    toast('err', 'Error al consultar status', e.message, { persist: true });
  }
}

function renderChannels(s) {
  const oc = s.orderChannel ?? {};
  const ec = s.eventChannel ?? {};
  const q = s.fifoQueue ?? {};
  $('#chanGrid').innerHTML = `
    <div class="chan-card">
      <h3>Pedidos · TCP 9801 <fluent-badge appearance="tint" color="${badgeForEstado(oc.estado ?? oc.state)}">${escapeHtml(oc.estado ?? oc.state ?? '?')}</fluent-badge></h3>
      ${statRow('Endpoint', oc.endpoint ?? '—')}
      ${statRow('Última conexión', fmtFecha(oc.ultimaConexionEn))}
      ${statRow('Última lectura', fmtFecha(oc.ultimaLecturaEn))}
      ${statRow('Bytes enviados', oc.bytesEnviados ?? '—')}
      ${statRow('Bytes recibidos', oc.bytesRecibidos ?? '—')}
      ${statRow('Reconexiones', oc.reconexiones ?? '—')}
      ${oc.error ? `<fluent-badge appearance="tint" color="danger" style="margin-top:8px">${escapeHtml(oc.error)}</fluent-badge>` : ''}
    </div>
    <div class="chan-card">
      <h3>Eventos · TCP 9802 <fluent-badge appearance="tint" color="${badgeForEstado(ec.estado ?? ec.state)}">${escapeHtml(ec.estado ?? ec.state ?? '?')}</fluent-badge></h3>
      ${statRow('Endpoint', ec.endpoint ?? '—')}
      ${statRow('Última conexión', fmtFecha(ec.ultimaConexionEn))}
      ${statRow('Última lectura', fmtFecha(ec.ultimaLecturaEn))}
      ${statRow('Bytes enviados', ec.bytesEnviados ?? '—')}
      ${statRow('Bytes recibidos', ec.bytesRecibidos ?? '—')}
      ${statRow('Reconexiones', ec.reconexiones ?? '—')}
      ${ec.error ? `<fluent-badge appearance="tint" color="danger" style="margin-top:8px">${escapeHtml(ec.error)}</fluent-badge>` : ''}
    </div>
    <div class="chan-card" style="grid-column: 1 / -1">
      <h3>Cola FIFO (RabbitMQ inbound) <fluent-badge appearance="tint" color="${q.reachable === false ? 'danger' : 'success'}">${q.reachable === false ? 'no alcanzable' : 'ok'}</fluent-badge></h3>
      ${statRow('Cola', q.queue ?? '—')}
      ${statRow('Pendientes', q.pendingCount ?? q.count ?? '—')}
      ${q.error ? `<fluent-badge appearance="tint" color="danger" style="margin-top:8px">${escapeHtml(q.error)}</fluent-badge>` : ''}
    </div>`;
}

function statRow(k, v) { return `<div class="chan-stat"><span class="k">${escapeHtml(k)}</span><span class="v">${escapeHtml(v)}</span></div>`; }

$('#btnChanReload').addEventListener('click', loadChannels);
$('#btnReconnect').addEventListener('click', async () => {
  const ok = await confirmDialog('Forzar reconexión', '<p>¿Cerrar y reabrir los sockets <code>9801</code> y <code>9802</code> contra KiSoft One?</p><p class="meta">KiSoft puede tardar unos segundos en responder.</p>');
  if (!ok) return;
  try { await apiCall(API.chanReconn, { allowEmpty: true }); toast('ok', 'Reconexión solicitada'); setTimeout(loadChannels, 2500); }
  catch (e) { toast('err', 'No se pudo reconectar', e.message, { persist: true }); }
});

// ============================================================
// Matriz
// ============================================================
// Refleja Matrix/MatrixAction.cs (Procesar=0, Ignorar=1, Deshabilitado=2) — System.Text.Json
// serializa el enum como su valor numérico, no como texto.
const MATRIX_ACTION_LABELS = ['Procesar', 'Ignorar', 'Deshabilitado'];
function matrixActionLabel(accion) { return MATRIX_ACTION_LABELS[accion] ?? `desconocido (${accion})`; }
function badgeForMatrixAction(accion) { return accion === 0 ? 'success' : accion === 1 ? 'warning' : accion === 2 ? 'danger' : 'subtle'; }

async function loadMatrix() {
  $('#matBody').innerHTML = '<tr><td colspan="5" class="meta" style="text-align:center;padding:30px">Cargando…</td></tr>';
  try {
    const rows = await apiCall(API.matList);
    state.matrix = rows;
    renderMatrix();
    setTabCount('mat', rows.length);
  } catch (e) {
    $('#matBody').innerHTML = `<tr><td colspan="5" class="meta" style="text-align:center;padding:30px;color:var(--err)">${escapeHtml(e.message)}</td></tr>`;
    toast('err', 'Error al listar matriz', e.message, { persist: true });
  }
}

function renderMatrix() {
  const filter = ($('#matSearch').value ?? '').toLowerCase().trim();
  const rows = filter ? state.matrix.filter(r => JSON.stringify(r).toLowerCase().includes(filter)) : state.matrix;
  $('#matCount').textContent = `${rows.length} de ${state.matrix.length}${filter ? ' (filtrado)' : ''}`;
  if (!rows.length) {
    $('#matBody').innerHTML = `<tr><td colspan="5" class="meta" style="text-align:center;padding:30px">Sin entradas.</td></tr>`;
    return;
  }
  $('#matBody').innerHTML = rows.map(r => `
    <tr>
      <td><code>${escapeHtml(r.emisor ?? '—')}</code></td>
      <td><code>${escapeHtml(r.tipoTelegrama ?? '—')}</code></td>
      <td><code>${escapeHtml(r.estacion ?? '—')}</code></td>
      <td><fluent-badge appearance="tint" color="${badgeForMatrixAction(r.accion)}">${escapeHtml(matrixActionLabel(r.accion))}</fluent-badge></td>
      <td class="meta">${metaCell(r.actualizadoEn)}</td>
    </tr>`).join('');
}

$('#matSearch').addEventListener('input', debounce(() => renderMatrix()));
$('#btnMatReload').addEventListener('click', async () => {
  try { await apiCall(API.matReload); toast('ok', 'Matriz recargada'); loadMatrix(); }
  catch (e) { toast('err', 'No se pudo recargar matriz', e.message, { persist: true }); }
});

// ---------- logout ----------
$('#logoutBtn').addEventListener('click', async () => {
  try { await API.authLogout(); } catch {}
  window.location.href = 'login.html';
});

// ---------- boot ----------
applyTheme();
{
  const isDev = document.title.includes('Development');
  const envBadge = $('#envBadge');
  envBadge.textContent = isDev ? 'dev' : 'prod';
  envBadge.setAttribute('color', isDev ? 'success' : 'warning');
  envBadge.setAttribute('appearance', 'tint');
  envBadge.title = isDev ? 'Entorno de desarrollo' : 'Entorno de producción — los cambios afectan datos reales';
}

(async () => {
  try {
    const r = await API.authMe();
    if (r.status === 401) { window.location.href = 'login.html'; return; }
    const session = await r.json();
    $('#userChip').textContent = session.username || '—';
  } catch {
    window.location.href = 'login.html';
    return;
  }
  setActiveTab('cfg');
})();
