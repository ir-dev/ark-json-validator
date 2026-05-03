from flask_sqlalchemy import SQLAlchemy
from datetime import datetime
import json

db = SQLAlchemy()


class Template(db.Model):
    __tablename__ = 'templates'

    id = db.Column(db.Integer, primary_key=True)
    name = db.Column(db.String(100), unique=True, nullable=False)
    description = db.Column(db.Text, default='')
    created_at = db.Column(db.DateTime, default=datetime.utcnow)
    updated_at = db.Column(db.DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    rules = db.relationship('ValidationRule', backref='template', cascade='all, delete-orphan', lazy=True)
    group_rules = db.relationship('GroupRule', backref='template', cascade='all, delete-orphan', lazy=True)

    def to_dict(self, include_rules=True):
        d = {
            'id': self.id,
            'name': self.name,
            'description': self.description,
            'created_at': self.created_at.isoformat() if self.created_at else None,
            'updated_at': self.updated_at.isoformat() if self.updated_at else None,
        }
        if include_rules:
            d['rules'] = [r.to_dict() for r in sorted(self.rules, key=lambda x: x.order_index)]
            d['group_rules'] = [g.to_dict() for g in self.group_rules]
        else:
            d['rule_count'] = len(self.rules)
            d['group_rule_count'] = len(self.group_rules)
        return d


class ValidationRule(db.Model):
    __tablename__ = 'validation_rules'

    id = db.Column(db.Integer, primary_key=True)
    template_id = db.Column(db.Integer, db.ForeignKey('templates.id'), nullable=False)
    field_path = db.Column(db.String(500), nullable=False)
    display_name = db.Column(db.String(200), default='')
    validations = db.Column(db.Text, nullable=False, default='[]')
    is_optional_field = db.Column(db.Boolean, default=False)
    order_index = db.Column(db.Integer, default=0)

    def to_dict(self):
        return {
            'id': self.id,
            'field_path': self.field_path,
            'display_name': self.display_name,
            'validations': json.loads(self.validations),
            'is_optional_field': self.is_optional_field,
            'order_index': self.order_index,
        }


class GroupRule(db.Model):
    __tablename__ = 'group_rules'

    id = db.Column(db.Integer, primary_key=True)
    template_id = db.Column(db.Integer, db.ForeignKey('templates.id'), nullable=False)
    name = db.Column(db.String(200), default='Group Rule')
    fields = db.Column(db.Text, nullable=False, default='[]')
    rule_type = db.Column(db.String(50), nullable=False)

    def to_dict(self):
        return {
            'id': self.id,
            'name': self.name,
            'fields': json.loads(self.fields),
            'rule_type': self.rule_type,
        }
