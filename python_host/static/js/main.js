'use strict';

// ── State ─────────────────────────────────────────────────────────────────
const S = {
  catalog: {},          // loaded from /ui/validation-catalog
  templates: [],        // list of templates from backend
  builder: {
    fieldRules: [],     // [{field_path, display_name, detected_type, validations, is_optional_field}]
    groupRules: [],     // [{name, fields, rule_type}]
    editingIndex: -1,   // which fieldRules[] is open in the modal
    editingGroupIndex: -1,
    editingTemplateId: null,
  },
};

// ── Category color map ────────────────────────────────────────────────────
const CAT_CLASS = {
  Basic: 'cat-basic', Format: 'cat-format', Type: 'cat-type',
  'Date/Time': 'cat-type', String: 'cat-string', Numeric: 'cat-numeric',
  Pattern: 'cat-pattern', SAP: 'cat-sap', OData: 'cat-odata',
};

// ── Bootstrap modal instances ─────────────────────────────────────────────
let ruleEditorModal, addFieldModal, groupRuleModal, deleteModal, mainToast;

// ── Init ──────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {
  ruleEditorModal = new bootstrap.Modal(document.getElementById('ruleEditorModal'));
  addFieldModal   = new bootstrap.Modal(document.getElementById('addFieldModal'));
  groupRuleModal  = new bootstrap.Modal(document.getElementById('groupRuleModal'));
  deleteModal     = new bootstrap.Modal(document.getElementById('deleteModal'));
  mainToast       = new bootstrap.Toast(document.getElementById('mainToast'), { delay: 3500 });

  // Wire nav tab clicks
  document.getElementById('mainTabs').addEventListener('click', e => {
    const link = e.target.closest('.ark-tab');
    if (link) { e.preventDefault(); switchTab(link.dataset.tab); }
  });

  await loadCatalog();
  await refreshTemplates();
  renderCatalog();
  switchTab('dashboard');
});

// ── Tab switching ─────────────────────────────────────────────────────────
function switchTab(tab) {
  document.querySelectorAll('.tab-section').forEach(s => s.classList.add('d-none'));
  document.querySelectorAll('.ark-tab').forEach(a => a.classList.remove('active'));

  const section = document.getElementById(`tab-${tab}`);
  if (section) section.classList.remove('d-none');
  const navLink = document.querySelector(`.ark-tab[data-tab="${tab}"]`);
  if (navLink) navLink.classList.add('active');

  if (tab === 'templates')  renderTemplatesGrid();
  if (tab === 'validate')   populateTemplateSelector();
  if (tab === 'dashboard')  renderDashboard();
}

// ── Catalog ───────────────────────────────────────────────────────────────
async function loadCatalog() {
  try {
    const res = await fetch('/ui/validation-catalog');
    S.catalog = await res.json();
  } catch {
    S.catalog = {};
  }
}

function renderCatalog() {
  const grid = document.getElementById('api-catalog-grid');
  if (!grid) return;
  grid.innerHTML = Object.entries(S.catalog).map(([key, meta]) => `
    <div class="catalog-item">
      <div class="catalog-item-key">${key}</div>
      <div class="catalog-item-label">${meta.label}</div>
      <div class="catalog-item-cat">${meta.category}</div>
    </div>
  `).join('');
}

// ── Template CRUD (backend) ───────────────────────────────────────────────
async function refreshTemplates() {
  try {
    const res = await fetch('/ui/templates');
    S.templates = await res.json();
  } catch {
    S.templates = [];
  }
}

async function fetchTemplateDetail(id) {
  const res = await fetch(`/ui/templates/${id}`);
  if (!res.ok) throw new Error('Not found');
  return res.json();
}

// ── Dashboard ─────────────────────────────────────────────────────────────
function renderDashboard() {
  const totalRules = S.templates.reduce((s, t) => s + (t.rule_count || 0), 0);
  document.getElementById('dash-template-count').textContent = S.templates.length;
  document.getElementById('dash-rule-count').textContent = totalRules;

  const container = document.getElementById('dash-recent-templates');
  if (!S.templates.length) {
    container.innerHTML = '<div class="empty-state-sm"><i class="fas fa-inbox"></i><br>No templates yet</div>';
    return;
  }
  container.innerHTML = S.templates.slice(0, 6).map(t => `
    <div class="recent-template-item" onclick="switchTab('templates')">
      <div class="rt-icon"><i class="fas fa-layer-group"></i></div>
      <div class="rt-name">${esc(t.name)}</div>
      <div class="rt-count">${t.rule_count} rule${t.rule_count !== 1 ? 's' : ''}</div>
      <i class="fas fa-chevron-right" style="color:var(--text-muted);font-size:11px"></i>
    </div>
  `).join('');
}

// ── Template Builder ───────────────────────────────────────────────────────

const SAMPLES = {
  generic: {
    customer: { id: 123, name: 'John Doe', email: 'john@example.com', phone: '+1-555-0100' },
    order: { amount: 199.99, currency: 'USD', date: '2024-01-15', status: 'ACTIVE' },
    shipping: { address: '123 Main St', city: 'Springfield', country: 'US', zip: '12345' }
  },
  sap_order: {
    SalesOrder: '0000012345',
    SoldToParty: '0001000001',
    CompanyCode: '1000',
    SalesOrganization: '1000',
    Plant: 'DE01',
    Material: 'MAT-001-A',
    Quantity: 10,
    NetAmount: '1500.00',
    Currency: 'EUR',
    UnitOfMeasure: 'EA',
    OrderDate: '/Date(1705276800000)/',
    RequestedDeliveryDate: '/Date(1707955200000)/',
    CostCenter: 'CC100',
    ProfitCenter: 'PC200',
    GLAccount: '0000400000',
    Guid: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'
  },
  sap_bp: {
    BusinessPartner: '0001000001',
    BusinessPartnerName: 'ACME Corporation',
    BusinessPartnerCategory: '2',
    CompanyCode: '1000',
    Currency: 'USD',
    Language: 'EN',
    EmailAddress: 'contact@acme.com',
    PhoneNumber: '+1-555-0200',
    PostalCode: '10001',
    Country: 'US',
    IsBlocked: false,
    CreatedAt: '/Date(1672531200000)/',
    ValidFrom: '2024-01-01',
    BPGuid: 'b2c3d4e5-f6a7-8901-bcde-f12345678901'
  }
};

function loadSample(key) {
  document.getElementById('builder-json-input').value = JSON.stringify(SAMPLES[key], null, 2);
}

function formatBuilderJSON() { formatTextarea('builder-json-input'); }
function formatValidateJSON() { formatTextarea('validate-json-input'); }

function formatTextarea(id) {
  const el = document.getElementById(id);
  try {
    el.value = JSON.stringify(JSON.parse(el.value), null, 2);
  } catch {
    showToast('Invalid JSON — cannot format', 'error');
  }
}

function handleBuilderUpload(e) {
  const file = e.target.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = ev => {
    document.getElementById('builder-json-input').value = ev.target.result;
    e.target.value = '';
  };
  reader.readAsText(file);
}

function handleValidateUpload(e) {
  const file = e.target.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = ev => {
    document.getElementById('validate-json-input').value = ev.target.result;
    e.target.value = '';
  };
  reader.readAsText(file);
}

function extractFields() {
  const raw = document.getElementById('builder-json-input').value.trim();
  if (!raw) { showToast('Please paste or load JSON first', 'error'); return; }

  let parsed;
  try { parsed = JSON.parse(raw); }
  catch (err) { showToast(`Invalid JSON: ${err.message}`, 'error'); return; }

  const detected = [];
  walkJSON(parsed, '', detected);

  // Merge with existing fieldRules — keep rules for paths that still exist
  const existingMap = {};
  S.builder.fieldRules.forEach(r => { existingMap[r.field_path] = r; });

  S.builder.fieldRules = detected.map(d => ({
    field_path: d.path,
    display_name: existingMap[d.path]?.display_name ?? '',
    detected_type: d.type,
    validations: existingMap[d.path]?.validations ?? [],
    is_optional_field: existingMap[d.path]?.is_optional_field ?? false,
  }));

  document.getElementById('builder-field-count').textContent = `(${S.builder.fieldRules.length} fields)`;

  showBuilderSteps();
  renderFieldsTable();
  showToast(`Extracted ${S.builder.fieldRules.length} fields`, 'success');
}

function walkJSON(obj, prefix, out) {
  if (obj === null || obj === undefined) {
    out.push({ path: prefix, type: 'null' }); return;
  }
  if (Array.isArray(obj)) {
    if (obj.length > 0) walkJSON(obj[0], prefix ? `${prefix}[0]` : '[0]', out);
    else out.push({ path: prefix, type: 'array[]' });
    return;
  }
  if (typeof obj === 'object') {
    for (const [k, v] of Object.entries(obj)) {
      walkJSON(v, prefix ? `${prefix}.${k}` : k, out);
    }
    return;
  }
  out.push({ path: prefix, type: detectType(obj, prefix) });
}

function detectType(val, path) {
  const s = String(val);
  if (typeof val === 'boolean') return 'boolean';
  if (typeof val === 'number') return Number.isInteger(val) ? 'integer' : 'decimal';
  if (/^\/Date\(\d+([+-]\d+)?\)\/$/.test(s)) return 'sap_date';
  if (/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(s)) return 'uuid';
  if (/^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$/.test(s)) return 'email';
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return 'date';
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(s)) return 'datetime';
  if (/^\+?[\d\s\-\.\(\)]{7,20}$/.test(s.trim()) && s.replace(/\D/g,'').length >= 7) return 'phone';
  if (/^\d{1,4}$/.test(s) && ['plant','company','code','org'].some(k => path.toLowerCase().includes(k))) return 'sap_code';
  return 'string';
}

function showBuilderSteps() {
  ['builder-step2', 'builder-step3', 'builder-step4'].forEach(id => {
    document.getElementById(id).classList.remove('d-none');
  });
}

function renderFieldsTable() {
  const tbody = document.getElementById('builder-fields-tbody');
  tbody.innerHTML = S.builder.fieldRules.map((r, i) => `
    <tr>
      <td>
        <code class="text-small" style="font-size:11.5px">${esc(r.field_path)}</code>
        ${r.is_optional_field ? '<span class="ms-1" style="font-size:10px;color:var(--text-muted)">(optional)</span>' : ''}
      </td>
      <td>
        <span class="text-muted" style="font-size:12.5px">${esc(r.display_name) || '<span style="opacity:.4">—</span>'}</span>
      </td>
      <td><span class="type-badge">${esc(r.detected_type || '?')}</span></td>
      <td>
        <div class="d-flex flex-wrap gap-1">
          ${r.validations.length === 0 ? '<span style="color:var(--text-muted);font-size:12px">No rules</span>' : ''}
          ${r.validations.map(v => ruleChip(v)).join('')}
        </div>
      </td>
      <td>
        <div class="d-flex gap-1">
          <button class="btn btn-xs btn-outline-primary" onclick="openRuleEditor(${i})" title="Edit rules"><i class="fas fa-sliders"></i></button>
          <button class="btn btn-xs btn-outline-danger" onclick="removeFieldRule(${i})" title="Remove field"><i class="fas fa-trash"></i></button>
        </div>
      </td>
    </tr>
  `).join('') || '<tr><td colspan="5" class="text-center text-muted py-4">No fields</td></tr>';
}

function ruleChip(v) {
  const meta = S.catalog[v.type] || {};
  const cls = CAT_CLASS[meta.category] || '';
  const label = meta.label || v.type;
  const short = label.replace(/^OData |^SAP /i, '').split('(')[0].trim();
  return `<span class="rule-chip ${cls}" title="${esc(label)}">${esc(short)}</span>`;
}

function removeFieldRule(i) {
  S.builder.fieldRules.splice(i, 1);
  document.getElementById('builder-field-count').textContent = `(${S.builder.fieldRules.length} fields)`;
  renderFieldsTable();
}

function openAddFieldModal() {
  document.getElementById('af-path').value = '';
  document.getElementById('af-display').value = '';
  addFieldModal.show();
}

function confirmAddField() {
  const path = document.getElementById('af-path').value.trim();
  if (!path) { showToast('Field path is required', 'error'); return; }
  if (S.builder.fieldRules.some(r => r.field_path === path)) {
    showToast('Field path already exists', 'error'); return;
  }
  S.builder.fieldRules.push({
    field_path: path,
    display_name: document.getElementById('af-display').value.trim(),
    detected_type: 'string',
    validations: [],
    is_optional_field: false,
  });
  renderFieldsTable();
  addFieldModal.hide();
  showToast('Field added', 'success');
}

// ── Rule Editor Modal ─────────────────────────────────────────────────────
function openRuleEditor(index) {
  S.builder.editingIndex = index;
  const r = S.builder.fieldRules[index];
  document.getElementById('re-field-path').value = r.field_path;
  document.getElementById('re-display-name').value = r.display_name || '';
  document.getElementById('re-optional').checked = r.is_optional_field || false;

  renderRulesList(r.validations);
  ruleEditorModal.show();
}

function renderRulesList(validations) {
  const list = document.getElementById('re-rules-list');
  const empty = document.getElementById('re-rules-empty');

  if (!validations.length) {
    list.innerHTML = '';
    empty.style.display = 'block';
    return;
  }
  empty.style.display = 'none';
  list.innerHTML = validations.map((v, i) => buildRuleRow(v, i)).join('');

  // Attach change handlers
  list.querySelectorAll('.rule-type-select').forEach(sel => {
    sel.addEventListener('change', () => onRuleTypeChange(sel));
  });
}

function buildRuleRow(v, i) {
  const categorized = groupedCatalog();
  const options = Object.entries(categorized).map(([cat, items]) => `
    <optgroup label="${cat}">
      ${items.map(([k, m]) => `<option value="${k}" ${k === v.type ? 'selected' : ''}>${m.label}</option>`).join('')}
    </optgroup>
  `).join('');

  const params = S.catalog[v.type]?.params || [];
  const paramsHTML = params.map(p => {
    const val = v[p.name] !== undefined ? v[p.name] : '';
    return `<input class="form-control form-control-sm rule-param"
              type="${p.type}" name="${p.name}" placeholder="${esc(p.label)}"
              value="${esc(String(val))}" data-rule-index="${i}" />`;
  }).join('');

  return `
    <div class="rule-row" data-rule-index="${i}">
      <select class="form-select form-select-sm rule-type-select" data-rule-index="${i}">${options}</select>
      <div class="rule-row-params">${paramsHTML}</div>
      <button class="btn btn-xs btn-outline-danger flex-shrink-0" onclick="removeRuleRow(${i})">
        <i class="fas fa-times"></i>
      </button>
    </div>
  `;
}

function groupedCatalog() {
  const groups = {};
  for (const [k, meta] of Object.entries(S.catalog)) {
    if (!groups[meta.category]) groups[meta.category] = [];
    groups[meta.category].push([k, meta]);
  }
  return groups;
}

function onRuleTypeChange(sel) {
  const i = parseInt(sel.dataset.ruleIndex, 10);
  const newType = sel.value;
  const params = S.catalog[newType]?.params || [];
  const row = sel.closest('.rule-row');
  const paramsDiv = row.querySelector('.rule-row-params');
  paramsDiv.innerHTML = params.map(p => `
    <input class="form-control form-control-sm rule-param"
      type="${p.type}" name="${p.name}" placeholder="${esc(p.label)}"
      data-rule-index="${i}" />
  `).join('');
}

function addRuleRow() {
  const list = document.getElementById('re-rules-list');
  const empty = document.getElementById('re-rules-empty');
  const nextIndex = list.querySelectorAll('.rule-row').length;

  empty.style.display = 'none';

  // Default rule: 'required'
  const defaultType = 'required';
  const tmpV = { type: defaultType };
  const div = document.createElement('div');
  div.innerHTML = buildRuleRow(tmpV, nextIndex);
  const rowEl = div.firstElementChild;
  list.appendChild(rowEl);
  rowEl.querySelector('.rule-type-select').addEventListener('change', e => onRuleTypeChange(e.target));
}

function removeRuleRow(i) {
  const list = document.getElementById('re-rules-list');
  const rows = list.querySelectorAll('.rule-row');
  if (rows[i]) rows[i].remove();
  // re-index
  list.querySelectorAll('.rule-row').forEach((row, idx) => {
    row.setAttribute('data-rule-index', idx);
    row.querySelectorAll('[data-rule-index]').forEach(el => el.setAttribute('data-rule-index', idx));
  });
  if (!list.querySelectorAll('.rule-row').length) {
    document.getElementById('re-rules-empty').style.display = 'block';
  }
}

function saveRuleEditor() {
  const i = S.builder.editingIndex;
  if (i < 0) return;

  const list = document.getElementById('re-rules-list');
  const validations = [];
  list.querySelectorAll('.rule-row').forEach(row => {
    const type = row.querySelector('.rule-type-select').value;
    const rule = { type };
    row.querySelectorAll('.rule-param').forEach(inp => {
      if (inp.value.trim() !== '') {
        const val = inp.type === 'number' ? parseFloat(inp.value) : inp.value.trim();
        rule[inp.name] = val;
      }
    });
    validations.push(rule);
  });

  S.builder.fieldRules[i].validations = validations;
  S.builder.fieldRules[i].display_name = document.getElementById('re-display-name').value.trim();
  S.builder.fieldRules[i].is_optional_field = document.getElementById('re-optional').checked;

  renderFieldsTable();
  ruleEditorModal.hide();
  showToast('Rules saved', 'success');
}

// ── Group Rules ───────────────────────────────────────────────────────────
function openGroupRuleModal(editIndex = -1) {
  S.builder.editingGroupIndex = editIndex;
  if (editIndex >= 0) {
    const gr = S.builder.groupRules[editIndex];
    document.getElementById('gr-name').value = gr.name || '';
    document.getElementById('gr-type').value = gr.rule_type;
    document.getElementById('gr-fields-container').innerHTML = '';
    gr.fields.forEach(f => addGrField(f));
  } else {
    document.getElementById('gr-name').value = '';
    document.getElementById('gr-type').value = 'at_least_one';
    document.getElementById('gr-fields-container').innerHTML = '';
    addGrField(); addGrField();
  }
  groupRuleModal.show();
}

function addGrField(value = '') {
  const container = document.getElementById('gr-fields-container');
  const paths = S.builder.fieldRules.map(r => r.field_path);
  const options = paths.map(p => `<option value="${esc(p)}" ${p === value ? 'selected' : ''}>${esc(p)}</option>`).join('');
  const isCustom = value && !paths.includes(value);
  const div = document.createElement('div');
  div.className = 'd-flex gap-2 align-items-center';
  div.innerHTML = `
    <select class="form-select form-select-sm gr-field-select flex-grow-1">
      <option value="">— choose field —</option>
      ${options}
      ${isCustom ? `<option value="${esc(value)}" selected>${esc(value)}</option>` : ''}
      <option value="__custom__">+ Custom path...</option>
    </select>
    <button class="btn btn-xs btn-outline-danger" onclick="this.parentElement.remove()"><i class="fas fa-times"></i></button>
  `;
  div.querySelector('select').addEventListener('change', e => {
    if (e.target.value === '__custom__') {
      const custom = prompt('Enter field path:');
      if (custom) {
        const opt = document.createElement('option');
        opt.value = custom; opt.textContent = custom; opt.selected = true;
        e.target.insertBefore(opt, e.target.lastElementChild);
        e.target.value = custom;
      } else e.target.value = '';
    }
  });
  container.appendChild(div);
}

function saveGroupRule() {
  const fields = [...document.querySelectorAll('.gr-field-select')]
    .map(s => s.value).filter(v => v && v !== '__custom__');
  if (fields.length < 2) { showToast('Select at least 2 fields', 'error'); return; }

  const gr = {
    name: document.getElementById('gr-name').value.trim() || 'Group Rule',
    rule_type: document.getElementById('gr-type').value,
    fields,
  };

  const i = S.builder.editingGroupIndex;
  if (i >= 0) S.builder.groupRules[i] = gr;
  else S.builder.groupRules.push(gr);

  renderGroupRules();
  groupRuleModal.hide();
  showToast('Group rule saved', 'success');
}

function renderGroupRules() {
  const container = document.getElementById('group-rules-list');
  const empty = document.getElementById('group-rules-empty');
  if (!S.builder.groupRules.length) {
    container.innerHTML = '';
    empty.style.display = 'block';
    return;
  }
  empty.style.display = 'none';
  const TYPE_LABELS = {
    at_least_one: 'At least one of',
    exactly_one: 'Exactly one of',
    all_or_none: 'All or none of',
    mutually_exclusive: 'Mutually exclusive',
  };
  container.innerHTML = S.builder.groupRules.map((gr, i) => `
    <div class="group-rule-card">
      <div>
        <div class="fw-semibold mb-1">${esc(gr.name)}</div>
        <div class="gr-type-badge">${TYPE_LABELS[gr.rule_type] || gr.rule_type}</div>
      </div>
      <div class="gr-fields">${gr.fields.map(f => `<code style="font-size:11px">${esc(f)}</code>`).join(', ')}</div>
      <div class="d-flex gap-1 ms-auto">
        <button class="btn btn-xs btn-outline-primary" onclick="openGroupRuleModal(${i})"><i class="fas fa-pen"></i></button>
        <button class="btn btn-xs btn-outline-danger" onclick="removeGroupRule(${i})"><i class="fas fa-trash"></i></button>
      </div>
    </div>
  `).join('');
}

function removeGroupRule(i) {
  S.builder.groupRules.splice(i, 1);
  renderGroupRules();
}

// ── Save / Load Template ──────────────────────────────────────────────────
async function saveTemplate() {
  const name = document.getElementById('template-name-input').value.trim();
  if (!name) { showToast('Template name is required', 'error'); return; }
  if (!S.builder.fieldRules.length) { showToast('Add at least one field rule', 'error'); return; }

  const payload = {
    name,
    description: document.getElementById('template-desc-input').value.trim(),
    rules: S.builder.fieldRules.map(r => ({
      field_path: r.field_path,
      display_name: r.display_name,
      validations: r.validations,
      is_optional_field: r.is_optional_field,
    })),
    group_rules: S.builder.groupRules,
  };

  const editId = S.builder.editingTemplateId;
  const url = editId ? `/ui/templates/${editId}` : '/ui/templates';
  const method = editId ? 'PUT' : 'POST';

  try {
    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const data = await res.json();
    if (!res.ok) { showToast(data.error || 'Save failed', 'error'); return; }

    showToast(`Template "${name}" ${editId ? 'updated' : 'saved'}!`, 'success');
    document.getElementById('save-status').textContent = `Saved as "${name}" (ID: ${data.id})`;
    S.builder.editingTemplateId = data.id;
    document.getElementById('builder-title').textContent = 'Edit Template';

    await refreshTemplates();
    renderDashboard();
  } catch (err) {
    showToast('Network error: ' + err.message, 'error');
  }
}

function resetBuilder() {
  S.builder.fieldRules = [];
  S.builder.groupRules = [];
  S.builder.editingIndex = -1;
  S.builder.editingTemplateId = null;

  document.getElementById('builder-json-input').value = '';
  document.getElementById('template-name-input').value = '';
  document.getElementById('template-desc-input').value = '';
  document.getElementById('builder-title').textContent = 'Template Builder';
  document.getElementById('builder-subtitle').textContent = 'Extract fields from JSON and assign validation rules';
  document.getElementById('save-status').textContent = '';

  ['builder-step2', 'builder-step3', 'builder-step4'].forEach(id => {
    document.getElementById(id).classList.add('d-none');
  });
  renderFieldsTable();
  renderGroupRules();
}

async function editTemplate(id) {
  try {
    const tmpl = await fetchTemplateDetail(id);
    resetBuilder();

    document.getElementById('template-name-input').value = tmpl.name;
    document.getElementById('template-desc-input').value = tmpl.description || '';
    document.getElementById('builder-title').textContent = `Edit: ${tmpl.name}`;
    document.getElementById('save-status').textContent = `Editing ID: ${tmpl.id}`;
    S.builder.editingTemplateId = tmpl.id;

    S.builder.fieldRules = tmpl.rules.map(r => ({
      field_path: r.field_path,
      display_name: r.display_name,
      detected_type: 'string',
      validations: r.validations,
      is_optional_field: r.is_optional_field,
    }));
    S.builder.groupRules = tmpl.group_rules.map(g => ({
      name: g.name,
      fields: g.fields,
      rule_type: g.rule_type,
    }));

    document.getElementById('builder-field-count').textContent = `(${S.builder.fieldRules.length} fields)`;
    showBuilderSteps();
    renderFieldsTable();
    renderGroupRules();
    switchTab('builder');
  } catch (err) {
    showToast('Failed to load template', 'error');
  }
}

// ── Templates Grid ────────────────────────────────────────────────────────
function renderTemplatesGrid() {
  const grid = document.getElementById('templates-grid');
  if (!S.templates.length) {
    grid.innerHTML = '<div class="col-12"><div class="empty-state"><i class="fas fa-layer-group fa-3x mb-3"></i><br>No templates yet.<br><small>Use the Template Builder to create one.</small></div></div>';
    return;
  }
  grid.innerHTML = S.templates.map(t => `
    <div class="col-sm-6 col-lg-4">
      <div class="template-card">
        <div class="template-card-name">${esc(t.name)}</div>
        <div class="template-card-desc">${esc(t.description) || '<span style="opacity:.4">No description</span>'}</div>
        <div class="template-card-meta">
          <span class="template-meta-item"><i class="fas fa-list-check"></i>${t.rule_count} rule${t.rule_count !== 1 ? 's' : ''}</span>
          <span class="template-meta-item"><i class="fas fa-object-group"></i>${t.group_rule_count} group${t.group_rule_count !== 1 ? 's' : ''}</span>
          <span class="template-meta-item"><i class="fas fa-calendar"></i>${formatDate(t.updated_at)}</span>
        </div>
        <div class="template-card-actions">
          <button class="btn btn-xs btn-primary" onclick="validateWithTemplate(${t.id})"><i class="fas fa-play me-1"></i>Validate</button>
          <button class="btn btn-xs btn-outline-secondary" onclick="editTemplate(${t.id})"><i class="fas fa-pen me-1"></i>Edit</button>
          <button class="btn btn-xs btn-outline-danger" onclick="confirmDelete(${t.id}, '${esc(t.name)}')"><i class="fas fa-trash me-1"></i>Delete</button>
        </div>
      </div>
    </div>
  `).join('');
}

function validateWithTemplate(id) {
  populateTemplateSelector(id);
  switchTab('validate');
}

// ── Delete ────────────────────────────────────────────────────────────────
function confirmDelete(id, name) {
  document.getElementById('delete-template-name').textContent = name;
  const btn = document.getElementById('delete-confirm-btn');
  btn.onclick = () => doDelete(id);
  deleteModal.show();
}

async function doDelete(id) {
  try {
    const res = await fetch(`/ui/templates/${id}`, { method: 'DELETE' });
    const data = await res.json();
    deleteModal.hide();
    showToast(data.message || 'Deleted', 'success');
    await refreshTemplates();
    renderTemplatesGrid();
    renderDashboard();
  } catch (err) {
    showToast('Delete failed: ' + err.message, 'error');
  }
}

// ── Validation ────────────────────────────────────────────────────────────
function populateTemplateSelector(selectId = null) {
  const sel = document.getElementById('validate-template-select');
  sel.innerHTML = '<option value="">— choose a template —</option>' +
    S.templates.map(t => `<option value="${t.id}" ${t.id === selectId ? 'selected' : ''}>${esc(t.name)}</option>`).join('');
}

async function runValidation() {
  const tid = document.getElementById('validate-template-select').value;
  if (!tid) { showToast('Select a template first', 'error'); return; }

  const raw = document.getElementById('validate-json-input').value.trim();
  if (!raw) { showToast('Paste a JSON payload first', 'error'); return; }

  let payload;
  try { payload = JSON.parse(raw); }
  catch (err) { showToast(`Invalid JSON: ${err.message}`, 'error'); return; }

  try {
    const res = await fetch(`/ui/validate/${tid}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const result = await res.json();
    renderValidationResults(result);
  } catch (err) {
    showToast('Request failed: ' + err.message, 'error');
  }
}

function renderValidationResults(result) {
  const panel = document.getElementById('validation-results-panel');
  const badge = document.getElementById('validation-status-badge');

  badge.innerHTML = result.valid
    ? '<span class="vbadge-pass"><i class="fas fa-check-circle me-1"></i>VALID</span>'
    : '<span class="vbadge-fail"><i class="fas fa-times-circle me-1"></i>INVALID</span>';

  const s = result.summary;
  const summaryHTML = `
    <div class="result-summary">
      <div class="result-pill total"><i class="fas fa-list"></i>${s.total_fields} fields</div>
      <div class="result-pill passed"><i class="fas fa-check"></i>${s.passed} passed</div>
      ${s.failed > 0 ? `<div class="result-pill failed"><i class="fas fa-times"></i>${s.failed} failed</div>` : ''}
      ${s.group_rules_total > 0 ? `
        <div class="result-pill ${s.group_rules_failed > 0 ? 'failed' : 'passed'}">
          <i class="fas fa-object-group"></i>${s.group_rules_passed}/${s.group_rules_total} group rules
        </div>` : ''}
    </div>
  `;

  const fieldsHTML = result.field_results.map(r => `
    <div class="result-field ${r.valid ? 'pass' : 'fail'}">
      <div class="result-field-header">
        <i class="fas fa-${r.valid ? 'check-circle text-success' : 'times-circle text-danger'}"></i>
        <span class="result-field-name">${esc(r.display_name || r.field_path)}</span>
        ${r.display_name && r.display_name !== r.field_path ? `<span class="result-field-path">${esc(r.field_path)}</span>` : ''}
        <span class="ms-auto result-field-value" title="${esc(String(r.value))}">${
          r.found ? (r.value === null ? '<em>null</em>' : esc(JSON.stringify(r.value)).slice(0, 60)) : '<em style="color:var(--danger)">not found</em>'
        }</span>
      </div>
      ${r.errors.length ? `<div class="result-field-errors">${r.errors.map(e => `<div class="result-error-item">${esc(e)}</div>`).join('')}</div>` : ''}
    </div>
  `).join('');

  const groupsHTML = result.group_results.length ? `
    <div class="mt-3">
      <div class="fw-semibold mb-2" style="font-size:13px;color:var(--text-muted)">Group Rules</div>
      ${result.group_results.map(g => `
        <div class="result-group ${g.valid ? 'pass' : 'fail'}">
          <div class="d-flex align-items-center gap-2">
            <i class="fas fa-${g.valid ? 'check-circle text-success' : 'times-circle text-danger'}"></i>
            <strong>${esc(g.name)}</strong>
            <span class="gr-type-badge">${esc(g.rule_type)}</span>
          </div>
          ${g.error ? `<div class="result-error-item mt-1">${esc(g.error)}</div>` : ''}
        </div>
      `).join('')}
    </div>
  ` : '';

  panel.innerHTML = summaryHTML + fieldsHTML + groupsHTML;
}

// ── Toast ─────────────────────────────────────────────────────────────────
function showToast(msg, type = 'info') {
  const el = document.getElementById('mainToast');
  const body = document.getElementById('toast-body');
  el.classList.remove('toast-success', 'toast-error', 'toast-info');
  el.classList.add(`toast-${type}`);
  body.innerHTML = `<i class="fas fa-${type === 'success' ? 'check-circle text-success' : type === 'error' ? 'times-circle text-danger' : 'info-circle text-info'} me-2"></i>${esc(msg)}`;
  mainToast.show();
}

// ── Helpers ───────────────────────────────────────────────────────────────
function esc(str) {
  if (str === null || str === undefined) return '';
  return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  return isNaN(d) ? '—' : d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}
