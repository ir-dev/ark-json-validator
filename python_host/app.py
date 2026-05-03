import os
import json
import base64
from functools import wraps

from flask import Flask, request, jsonify, render_template, abort
from models import db, Template, ValidationRule, GroupRule
from validators import validate_json_against_template, VALIDATION_CATALOG

app = Flask(__name__)

# On Azure App Service, SQLITE_DB_PATH should be set to /home/validator.db
# /home is the only persistent directory across restarts and deployments.
_db_path = os.environ.get(
    'SQLITE_DB_PATH',
    os.path.join(os.path.dirname(os.path.abspath(__file__)), 'instance', 'validator.db'),
)
os.makedirs(os.path.dirname(_db_path), exist_ok=True)
app.config['SQLALCHEMY_DATABASE_URI'] = f'sqlite:///{_db_path}'
app.config['SQLALCHEMY_TRACK_MODIFICATIONS'] = False
app.config['SECRET_KEY'] = os.environ.get('SECRET_KEY', 'dev-secret-change-in-prod')

API_USERNAME = os.environ.get('API_USERNAME', 'admin')
API_PASSWORD = os.environ.get('API_PASSWORD', 'admin123')

db.init_app(app)

with app.app_context():
    db.create_all()


# ── Auth helpers ──────────────────────────────────────────────────────────────

def check_basic_auth():
    auth = request.headers.get('Authorization', '')
    if not auth.startswith('Basic '):
        return False
    try:
        username, password = base64.b64decode(auth[6:]).decode('utf-8').split(':', 1)
    except Exception:
        return False
    return username == API_USERNAME and password == API_PASSWORD


def require_basic_auth(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        if not check_basic_auth():
            return jsonify({'error': 'Authentication required'}), 401, {
                'WWW-Authenticate': 'Basic realm="JSON Validator API"'
            }
        return f(*args, **kwargs)
    return decorated


def _save_template_from_payload(data, template=None):
    """Create or update a Template from a request payload dict. Returns the saved Template."""
    is_new = template is None
    if is_new:
        template = Template(name=data['name'].strip(), description=data.get('description', ''))
        db.session.add(template)
        db.session.flush()
    else:
        template.name = data['name'].strip()
        template.description = data.get('description', '')
        ValidationRule.query.filter_by(template_id=template.id).delete()
        GroupRule.query.filter_by(template_id=template.id).delete()

    for i, r in enumerate(data.get('rules', [])):
        db.session.add(ValidationRule(
            template_id=template.id,
            field_path=r['field_path'],
            display_name=r.get('display_name', ''),
            validations=json.dumps(r.get('validations', [])),
            is_optional_field=r.get('is_optional_field', False),
            order_index=i,
        ))

    for g in data.get('group_rules', []):
        db.session.add(GroupRule(
            template_id=template.id,
            name=g.get('name', 'Group Rule'),
            fields=json.dumps(g.get('fields', [])),
            rule_type=g.get('rule_type', 'at_least_one'),
        ))

    db.session.commit()
    return template


# ── Web UI ────────────────────────────────────────────────────────────────────

@app.route('/')
def index():
    return render_template('index.html')


@app.route('/ui/validation-catalog')
def ui_catalog():
    return jsonify(VALIDATION_CATALOG)


# ── UI template endpoints (no auth — browser SPA) ─────────────────────────────

@app.route('/ui/templates', methods=['GET'])
def ui_list_templates():
    templates = Template.query.order_by(Template.updated_at.desc()).all()
    return jsonify([t.to_dict(include_rules=False) for t in templates])


@app.route('/ui/templates', methods=['POST'])
def ui_create_template():
    data = request.get_json(silent=True) or {}
    if not data.get('name', '').strip():
        return jsonify({'error': 'Template name is required'}), 400
    if Template.query.filter_by(name=data['name'].strip()).first():
        return jsonify({'error': f'Template "{data["name"]}" already exists'}), 409
    tmpl = _save_template_from_payload(data)
    return jsonify(tmpl.to_dict()), 201


@app.route('/ui/templates/<int:tid>', methods=['GET'])
def ui_get_template(tid):
    return jsonify(Template.query.get_or_404(tid).to_dict())


@app.route('/ui/templates/<int:tid>', methods=['PUT'])
def ui_update_template(tid):
    template = Template.query.get_or_404(tid)
    data = request.get_json(silent=True) or {}
    if not data.get('name', '').strip():
        return jsonify({'error': 'Template name is required'}), 400
    existing = Template.query.filter_by(name=data['name'].strip()).first()
    if existing and existing.id != tid:
        return jsonify({'error': f'Template "{data["name"]}" already exists'}), 409
    tmpl = _save_template_from_payload(data, template)
    return jsonify(tmpl.to_dict())


@app.route('/ui/templates/<int:tid>', methods=['DELETE'])
def ui_delete_template(tid):
    template = Template.query.get_or_404(tid)
    name = template.name
    db.session.delete(template)
    db.session.commit()
    return jsonify({'message': f'Template "{name}" deleted'})


@app.route('/ui/validate/<int:tid>', methods=['POST'])
def ui_validate(tid):
    template = Template.query.get_or_404(tid)
    data = request.get_json(silent=True)
    if data is None:
        return jsonify({'error': 'Invalid or missing JSON body'}), 400
    result = validate_json_against_template(data, template)
    return jsonify(result), (200 if result['valid'] else 422)


# ── REST API (Basic Auth required) ────────────────────────────────────────────

@app.route('/api/templates', methods=['GET'])
@require_basic_auth
def api_list_templates():
    templates = Template.query.order_by(Template.updated_at.desc()).all()
    return jsonify([t.to_dict(include_rules=False) for t in templates])


@app.route('/api/templates', methods=['POST'])
@require_basic_auth
def api_create_template():
    data = request.get_json(silent=True) or {}
    if not data.get('name', '').strip():
        return jsonify({'error': 'Template name is required'}), 400
    if Template.query.filter_by(name=data['name'].strip()).first():
        return jsonify({'error': f'Template "{data["name"]}" already exists'}), 409
    tmpl = _save_template_from_payload(data)
    return jsonify(tmpl.to_dict()), 201


@app.route('/api/templates/<int:tid>', methods=['GET'])
@require_basic_auth
def api_get_template(tid):
    return jsonify(Template.query.get_or_404(tid).to_dict())


@app.route('/api/templates/<int:tid>', methods=['PUT'])
@require_basic_auth
def api_update_template(tid):
    template = Template.query.get_or_404(tid)
    data = request.get_json(silent=True) or {}
    if not data.get('name', '').strip():
        return jsonify({'error': 'Template name is required'}), 400
    existing = Template.query.filter_by(name=data['name'].strip()).first()
    if existing and existing.id != tid:
        return jsonify({'error': f'Template "{data["name"]}" already exists'}), 409
    tmpl = _save_template_from_payload(data, template)
    return jsonify(tmpl.to_dict())


@app.route('/api/templates/<int:tid>', methods=['DELETE'])
@require_basic_auth
def api_delete_template(tid):
    template = Template.query.get_or_404(tid)
    name = template.name
    db.session.delete(template)
    db.session.commit()
    return jsonify({'message': f'Template "{name}" deleted'})


@app.route('/api/validate/<int:tid>', methods=['POST'])
@require_basic_auth
def api_validate_by_id(tid):
    template = Template.query.get_or_404(tid)
    data = request.get_json(silent=True)
    if data is None:
        return jsonify({'error': 'Request body must be valid JSON with Content-Type: application/json'}), 400
    result = validate_json_against_template(data, template)
    return jsonify(result), (200 if result['valid'] else 422)


@app.route('/api/validate/by-name/<path:name>', methods=['POST'])
@require_basic_auth
def api_validate_by_name(name):
    template = Template.query.filter_by(name=name).first()
    if not template:
        return jsonify({'error': f'Template "{name}" not found'}), 404
    data = request.get_json(silent=True)
    if data is None:
        return jsonify({'error': 'Request body must be valid JSON with Content-Type: application/json'}), 400
    result = validate_json_against_template(data, template)
    return jsonify(result), (200 if result['valid'] else 422)


if __name__ == '__main__':
    app.run(debug=True, port=5000)
