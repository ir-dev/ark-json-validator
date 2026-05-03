import re
import json
import base64
import decimal
from datetime import datetime
from typing import Any, Dict, List, Optional, Tuple

# ISO 4217 currency codes commonly used in SAP
SAP_CURRENCIES = {
    'USD', 'EUR', 'GBP', 'JPY', 'CHF', 'CAD', 'AUD', 'NZD', 'SEK', 'NOK',
    'DKK', 'HKD', 'SGD', 'TWD', 'KRW', 'MXN', 'BRL', 'INR', 'CNY', 'ZAR',
    'RUB', 'TRY', 'PLN', 'CZK', 'HUF', 'ILS', 'PHP', 'IDR', 'MYR', 'THB',
    'SAR', 'AED', 'QAR', 'KWD', 'BHD', 'OMR', 'EGP', 'NGN', 'PKR', 'BDT',
    'VND', 'UAH', 'RON', 'BGN', 'HRK', 'ISK', 'DZD', 'MAD', 'TND', 'LYD',
}

# SAP standard Units of Measure
SAP_UOMS = {
    'EA', 'PC', 'ST', 'PCS', 'KG', 'G', 'MG', 'T', 'LB', 'OZ', 'TON',
    'L', 'ML', 'M3', 'CM3', 'FT3', 'IN3', 'GAL', 'QT', 'PT', 'FL',
    'M', 'CM', 'MM', 'KM', 'FT', 'IN', 'YD', 'MI',
    'M2', 'CM2', 'FT2', 'IN2', 'YD2',
    'H', 'MIN', 'S', 'D', 'WK', 'MON', 'AN', 'HR',
    'PAL', 'ROL', 'BX', 'CS', 'CTN', 'PK', 'BAG', 'DZ', 'GR', 'PR',
    'SET', 'KIT', 'BTL', 'CAN', 'TUB', 'BOX', 'PAC', 'PLT', 'PK',
    'DOZ', 'HDR', 'CAR', 'SKD', 'BND', 'RLL', 'SHT', 'CBM', 'CWT',
}

VALIDATION_CATALOG = {
    # Basic checks
    'required':           {'label': 'Required (not null/empty)', 'category': 'Basic', 'params': []},
    'not_null':           {'label': 'Not Null', 'category': 'Basic', 'params': []},
    'not_empty':          {'label': 'Not Empty String / Collection', 'category': 'Basic', 'params': []},
    # Format checks
    'email':              {'label': 'Email Address', 'category': 'Format', 'params': []},
    'phone':              {'label': 'Phone Number', 'category': 'Format', 'params': []},
    'url':                {'label': 'URL / URI', 'category': 'Format', 'params': []},
    'uuid':               {'label': 'UUID Format', 'category': 'Format', 'params': []},
    'ipv4':               {'label': 'IPv4 Address', 'category': 'Format', 'params': []},
    'ipv6':               {'label': 'IPv6 Address', 'category': 'Format', 'params': []},
    # Type checks
    'numeric':            {'label': 'Numeric', 'category': 'Type', 'params': []},
    'integer':            {'label': 'Integer', 'category': 'Type', 'params': []},
    'boolean':            {'label': 'Boolean', 'category': 'Type', 'params': []},
    'string_type':        {'label': 'String Type', 'category': 'Type', 'params': []},
    'array_type':         {'label': 'Array Type', 'category': 'Type', 'params': []},
    'object_type':        {'label': 'Object Type', 'category': 'Type', 'params': []},
    # Date / Time
    'date':               {'label': 'Date (YYYY-MM-DD)', 'category': 'Date/Time', 'params': []},
    'datetime':           {'label': 'DateTime ISO 8601', 'category': 'Date/Time', 'params': []},
    'time':               {'label': 'Time (HH:MM:SS)', 'category': 'Date/Time', 'params': []},
    # String constraints
    'min_length':         {'label': 'Min Length', 'category': 'String', 'params': [{'name': 'value', 'label': 'Minimum Length', 'type': 'number'}]},
    'max_length':         {'label': 'Max Length', 'category': 'String', 'params': [{'name': 'value', 'label': 'Maximum Length', 'type': 'number'}]},
    'exact_length':       {'label': 'Exact Length', 'category': 'String', 'params': [{'name': 'value', 'label': 'Length', 'type': 'number'}]},
    'starts_with':        {'label': 'Starts With', 'category': 'String', 'params': [{'name': 'value', 'label': 'Prefix', 'type': 'text'}]},
    'ends_with':          {'label': 'Ends With', 'category': 'String', 'params': [{'name': 'value', 'label': 'Suffix', 'type': 'text'}]},
    # Numeric constraints
    'min_value':          {'label': 'Min Value', 'category': 'Numeric', 'params': [{'name': 'value', 'label': 'Minimum Value', 'type': 'number'}]},
    'max_value':          {'label': 'Max Value', 'category': 'Numeric', 'params': [{'name': 'value', 'label': 'Maximum Value', 'type': 'number'}]},
    # Pattern & enum
    'regex':              {'label': 'Regex Pattern', 'category': 'Pattern', 'params': [
                              {'name': 'pattern', 'label': 'Regex Pattern', 'type': 'text'},
                              {'name': 'message', 'label': 'Custom Error Message (optional)', 'type': 'text'},
                          ]},
    'enum':               {'label': 'Allowed Values (Enum)', 'category': 'Pattern', 'params': [
                              {'name': 'values', 'label': 'Allowed values (comma-separated)', 'type': 'text'},
                          ]},
    # SAP specific
    'sap_date':           {'label': 'SAP Date /Date(timestamp)/', 'category': 'SAP', 'params': []},
    'sap_guid':           {'label': 'SAP GUID', 'category': 'SAP', 'params': []},
    'sap_material':       {'label': 'SAP Material Number', 'category': 'SAP', 'params': []},
    'sap_plant':          {'label': 'SAP Plant Code (1-4 chars)', 'category': 'SAP', 'params': []},
    'sap_company_code':   {'label': 'SAP Company Code (1-4 chars)', 'category': 'SAP', 'params': []},
    'sap_currency':       {'label': 'SAP Currency Code (ISO)', 'category': 'SAP', 'params': []},
    'sap_uom':            {'label': 'SAP Unit of Measure', 'category': 'SAP', 'params': []},
    'sap_business_partner': {'label': 'SAP Business Partner Number', 'category': 'SAP', 'params': []},
    'sap_sales_order':    {'label': 'SAP Sales Order Number', 'category': 'SAP', 'params': []},
    'sap_purchase_order': {'label': 'SAP Purchase Order Number', 'category': 'SAP', 'params': []},
    'sap_cost_center':    {'label': 'SAP Cost Center', 'category': 'SAP', 'params': []},
    'sap_profit_center':  {'label': 'SAP Profit Center', 'category': 'SAP', 'params': []},
    'sap_gl_account':     {'label': 'SAP G/L Account', 'category': 'SAP', 'params': []},
    'sap_storage_location': {'label': 'SAP Storage Location (1-4 chars)', 'category': 'SAP', 'params': []},
    # OData EDM types
    'odata_edm_string':   {'label': 'OData Edm.String', 'category': 'OData', 'params': [
                              {'name': 'max_length', 'label': 'MaxLength (optional)', 'type': 'number'},
                          ]},
    'odata_edm_int16':    {'label': 'OData Edm.Int16', 'category': 'OData', 'params': []},
    'odata_edm_int32':    {'label': 'OData Edm.Int32', 'category': 'OData', 'params': []},
    'odata_edm_int64':    {'label': 'OData Edm.Int64', 'category': 'OData', 'params': []},
    'odata_edm_decimal':  {'label': 'OData Edm.Decimal', 'category': 'OData', 'params': [
                              {'name': 'precision', 'label': 'Precision (optional)', 'type': 'number'},
                              {'name': 'scale', 'label': 'Scale (optional)', 'type': 'number'},
                          ]},
    'odata_edm_single':   {'label': 'OData Edm.Single (float)', 'category': 'OData', 'params': []},
    'odata_edm_double':   {'label': 'OData Edm.Double', 'category': 'OData', 'params': []},
    'odata_edm_datetime': {'label': 'OData Edm.DateTime /Date(ts)/', 'category': 'OData', 'params': []},
    'odata_edm_datetimeoffset': {'label': 'OData Edm.DateTimeOffset (ISO)', 'category': 'OData', 'params': []},
    'odata_edm_boolean':  {'label': 'OData Edm.Boolean', 'category': 'OData', 'params': []},
    'odata_edm_guid':     {'label': 'OData Edm.Guid', 'category': 'OData', 'params': []},
    'odata_edm_binary':   {'label': 'OData Edm.Binary (base64)', 'category': 'OData', 'params': []},
    'odata_edm_byte':     {'label': 'OData Edm.Byte (0-255)', 'category': 'OData', 'params': []},
    'odata_edm_sbyte':    {'label': 'OData Edm.SByte (-128 to 127)', 'category': 'OData', 'params': []},
}


def get_value_by_path(data: Any, path: str) -> Tuple[Any, bool]:
    """Resolve a dot-notation path (supports array indexing [0] or [*]) in nested dicts/lists."""
    if not path:
        return data, True

    parts = re.split(r'\.(?![^\[]*\])', path)  # split on dots not inside brackets
    current = data

    for part in parts:
        array_match = re.match(r'^(\w+)\[(\d+|\*)\]$', part)
        if array_match:
            key, idx = array_match.group(1), array_match.group(2)
            if isinstance(current, dict) and key in current:
                current = current[key]
                if isinstance(current, list):
                    if idx == '*':
                        return current, True
                    try:
                        current = current[int(idx)]
                    except IndexError:
                        return None, False
                else:
                    return None, False
            else:
                return None, False
        elif isinstance(current, dict):
            if part not in current:
                return None, False
            current = current[part]
        elif isinstance(current, list):
            try:
                current = current[int(part)]
            except (ValueError, IndexError):
                return None, False
        else:
            return None, False

    return current, True


def is_empty(value: Any) -> bool:
    if value is None:
        return True
    if isinstance(value, str) and value.strip() == '':
        return True
    if isinstance(value, (list, dict)) and len(value) == 0:
        return True
    return False


def validate_single(value: Any, rule: Dict) -> Optional[str]:
    """Apply one rule to one value; return error string or None."""
    t = rule.get('type', '')

    # ── Basic ──────────────────────────────────────────────────────────────
    if t == 'required':
        if is_empty(value):
            return 'Field is required and cannot be null or empty'
        return None
    if t == 'not_null':
        if value is None:
            return 'Field cannot be null'
        return None
    if t == 'not_empty':
        if isinstance(value, str) and value.strip() == '':
            return 'Field cannot be an empty string'
        if isinstance(value, (list, dict)) and len(value) == 0:
            return 'Field cannot be empty'
        return None

    # Allow None to pass all remaining rules (use 'required' to enforce presence)
    if value is None:
        return None

    # ── Format ─────────────────────────────────────────────────────────────
    if t == 'email':
        if not re.match(r'^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$', str(value)):
            return f'Invalid email address: {value}'
    elif t == 'phone':
        if not re.match(r'^\+?[\d\s\-\.\(\)]{7,20}$', str(value).strip()):
            return f'Invalid phone number: {value}'
    elif t == 'url':
        if not re.match(r'^https?://[^\s/$.?#].[^\s]*$', str(value), re.IGNORECASE):
            return f'Invalid URL: {value}'
    elif t == 'uuid':
        if not re.match(r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$', str(value)):
            return f'Invalid UUID: {value}'
    elif t == 'ipv4':
        parts = str(value).split('.')
        if len(parts) != 4 or not all(p.isdigit() and 0 <= int(p) <= 255 for p in parts):
            return f'Invalid IPv4 address: {value}'
    elif t == 'ipv6':
        if not re.match(r'^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^::1$|^::$', str(value)):
            return f'Invalid IPv6 address: {value}'

    # ── Type ───────────────────────────────────────────────────────────────
    elif t == 'numeric':
        try:
            float(str(value))
        except ValueError:
            return f'Expected numeric value, got: {value}'
    elif t == 'integer':
        try:
            v = float(str(value))
            if v != int(v):
                raise ValueError()
            int(str(value).split('.')[0])
        except (ValueError, TypeError):
            return f'Expected integer, got: {value}'
    elif t == 'boolean':
        if not isinstance(value, bool) and str(value).lower() not in ('true', 'false', '0', '1'):
            return f'Expected boolean, got: {value}'
    elif t == 'string_type':
        if not isinstance(value, str):
            return f'Expected string type, got: {type(value).__name__}'
    elif t == 'array_type':
        if not isinstance(value, list):
            return f'Expected array type, got: {type(value).__name__}'
    elif t == 'object_type':
        if not isinstance(value, dict):
            return f'Expected object type, got: {type(value).__name__}'

    # ── Date/Time ──────────────────────────────────────────────────────────
    elif t == 'date':
        try:
            datetime.strptime(str(value), '%Y-%m-%d')
        except ValueError:
            return f'Invalid date (expected YYYY-MM-DD): {value}'
    elif t == 'datetime':
        ok = False
        for fmt in ('%Y-%m-%dT%H:%M:%SZ', '%Y-%m-%dT%H:%M:%S', '%Y-%m-%dT%H:%M:%S.%f',
                    '%Y-%m-%d %H:%M:%S', '%Y-%m-%dT%H:%M:%S.%fZ'):
            try:
                datetime.strptime(str(value)[:26], fmt[:len(str(value)[:26])])
                ok = True
                break
            except ValueError:
                continue
        if not ok:
            return f'Invalid ISO 8601 datetime: {value}'
    elif t == 'time':
        if not re.match(r'^\d{2}:\d{2}(:\d{2})?$', str(value)):
            return f'Invalid time (expected HH:MM or HH:MM:SS): {value}'

    # ── String constraints ─────────────────────────────────────────────────
    elif t == 'min_length':
        n = int(rule.get('value', 0))
        if len(str(value)) < n:
            return f'Length {len(str(value))} is below minimum {n}'
    elif t == 'max_length':
        n = int(rule.get('value', 255))
        if len(str(value)) > n:
            return f'Length {len(str(value))} exceeds maximum {n}'
    elif t == 'exact_length':
        n = int(rule.get('value', 0))
        if len(str(value)) != n:
            return f'Length must be exactly {n}, got {len(str(value))}'
    elif t == 'starts_with':
        prefix = str(rule.get('value', ''))
        if not str(value).startswith(prefix):
            return f'Value must start with "{prefix}"'
    elif t == 'ends_with':
        suffix = str(rule.get('value', ''))
        if not str(value).endswith(suffix):
            return f'Value must end with "{suffix}"'

    # ── Numeric constraints ────────────────────────────────────────────────
    elif t == 'min_value':
        try:
            if float(str(value)) < float(rule.get('value', 0)):
                return f'Value {value} is below minimum {rule.get("value")}'
        except (ValueError, TypeError):
            return f'Cannot apply min_value to non-numeric: {value}'
    elif t == 'max_value':
        try:
            if float(str(value)) > float(rule.get('value', 0)):
                return f'Value {value} exceeds maximum {rule.get("value")}'
        except (ValueError, TypeError):
            return f'Cannot apply max_value to non-numeric: {value}'

    # ── Pattern & enum ─────────────────────────────────────────────────────
    elif t == 'regex':
        pattern = rule.get('pattern', '')
        custom_msg = rule.get('message', '')
        try:
            if not re.match(pattern, str(value)):
                return custom_msg or f'Value does not match pattern "{pattern}": {value}'
        except re.error as e:
            return f'Invalid regex pattern ({e}): {pattern}'
    elif t == 'enum':
        allowed = [v.strip() for v in str(rule.get('values', '')).split(',') if v.strip()]
        if str(value) not in allowed:
            return f'"{value}" is not one of the allowed values: {", ".join(allowed)}'

    # ── SAP specific ───────────────────────────────────────────────────────
    elif t == 'sap_date':
        if not re.match(r'^/Date\(\d{10,13}([+-]\d{4})?\)/$', str(value)):
            return f'Invalid SAP OData date (expected /Date(timestamp)/): {value}'
    elif t == 'sap_guid':
        if not re.match(r'^[0-9A-Fa-f]{8}-?[0-9A-Fa-f]{4}-?[0-9A-Fa-f]{4}-?[0-9A-Fa-f]{4}-?[0-9A-Fa-f]{12}$', str(value)):
            return f'Invalid SAP GUID: {value}'
    elif t == 'sap_material':
        if len(str(value)) > 40:
            return f'SAP Material number exceeds 40 characters'
        if not re.match(r'^[A-Z0-9\-_./\s]+$', str(value).upper()):
            return f'Invalid SAP Material number (alphanumeric/special only): {value}'
    elif t == 'sap_plant':
        if not re.match(r'^[A-Z0-9]{1,4}$', str(value).upper()):
            return f'Invalid SAP Plant code (1-4 alphanumeric): {value}'
    elif t == 'sap_company_code':
        if not re.match(r'^[A-Z0-9]{1,4}$', str(value).upper()):
            return f'Invalid SAP Company Code (1-4 alphanumeric): {value}'
    elif t == 'sap_currency':
        if str(value).upper() not in SAP_CURRENCIES:
            return f'Unrecognized SAP currency code: {value}'
    elif t == 'sap_uom':
        if str(value).upper() not in SAP_UOMS:
            return f'Unrecognized SAP Unit of Measure: {value}'
    elif t == 'sap_business_partner':
        if not re.match(r'^\d{1,10}$', str(value)):
            return f'Invalid SAP Business Partner number (up to 10 digits): {value}'
    elif t == 'sap_sales_order':
        if not re.match(r'^\d{1,10}$', str(value)):
            return f'Invalid SAP Sales Order number (up to 10 digits): {value}'
    elif t == 'sap_purchase_order':
        if not re.match(r'^\d{1,10}$', str(value)):
            return f'Invalid SAP Purchase Order number (up to 10 digits): {value}'
    elif t == 'sap_cost_center':
        if not re.match(r'^[A-Z0-9]{1,10}$', str(value).upper()):
            return f'Invalid SAP Cost Center (up to 10 alphanumeric): {value}'
    elif t == 'sap_profit_center':
        if not re.match(r'^[A-Z0-9]{1,10}$', str(value).upper()):
            return f'Invalid SAP Profit Center (up to 10 alphanumeric): {value}'
    elif t == 'sap_gl_account':
        if not re.match(r'^\d{1,10}$', str(value)):
            return f'Invalid SAP G/L Account (up to 10 digits): {value}'
    elif t == 'sap_storage_location':
        if not re.match(r'^[A-Z0-9]{1,4}$', str(value).upper()):
            return f'Invalid SAP Storage Location (1-4 alphanumeric): {value}'

    # ── OData EDM types ────────────────────────────────────────────────────
    elif t == 'odata_edm_string':
        if not isinstance(value, str):
            return f'Edm.String requires string type, got: {type(value).__name__}'
        ml = rule.get('max_length')
        if ml and len(value) > int(ml):
            return f'Edm.String length {len(value)} exceeds MaxLength {ml}'
    elif t == 'odata_edm_byte':
        try:
            v = int(str(value))
            if not (0 <= v <= 255):
                return f'Edm.Byte must be 0-255, got: {v}'
        except (ValueError, TypeError):
            return f'Edm.Byte requires integer, got: {value}'
    elif t == 'odata_edm_sbyte':
        try:
            v = int(str(value))
            if not (-128 <= v <= 127):
                return f'Edm.SByte must be -128 to 127, got: {v}'
        except (ValueError, TypeError):
            return f'Edm.SByte requires integer, got: {value}'
    elif t == 'odata_edm_int16':
        try:
            v = int(str(value))
            if not (-32768 <= v <= 32767):
                return f'Edm.Int16 must be -32768 to 32767, got: {v}'
        except (ValueError, TypeError):
            return f'Edm.Int16 requires integer, got: {value}'
    elif t == 'odata_edm_int32':
        try:
            v = int(str(value))
            if not (-2147483648 <= v <= 2147483647):
                return f'Edm.Int32 out of range [-2147483648, 2147483647]: {v}'
        except (ValueError, TypeError):
            return f'Edm.Int32 requires integer, got: {value}'
    elif t == 'odata_edm_int64':
        try:
            v = int(str(value))
            if not (-9223372036854775808 <= v <= 9223372036854775807):
                return f'Edm.Int64 out of range'
        except (ValueError, TypeError):
            return f'Edm.Int64 requires integer, got: {value}'
    elif t == 'odata_edm_single':
        try:
            float(str(value))
        except (ValueError, TypeError):
            return f'Edm.Single requires float, got: {value}'
    elif t == 'odata_edm_double':
        try:
            float(str(value))
        except (ValueError, TypeError):
            return f'Edm.Double requires float, got: {value}'
    elif t == 'odata_edm_decimal':
        try:
            d = decimal.Decimal(str(value))
            prec = rule.get('precision')
            scale = rule.get('scale')
            if prec or scale:
                sign, digits, exp = d.as_tuple()
                dec_digits = max(0, -int(exp))
                total_digits = len(digits)
                if scale and dec_digits > int(scale):
                    return f'Edm.Decimal scale {dec_digits} exceeds allowed {scale}'
                if prec and total_digits > int(prec):
                    return f'Edm.Decimal precision {total_digits} exceeds allowed {prec}'
        except Exception:
            return f'Edm.Decimal requires decimal number, got: {value}'
    elif t == 'odata_edm_datetime':
        if not re.match(r'^/Date\(\d{10,13}([+-]\d{4})?\)/$', str(value)):
            return f'Edm.DateTime must be /Date(timestamp)/ format: {value}'
    elif t == 'odata_edm_datetimeoffset':
        if not re.match(r'^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$', str(value)):
            return f'Edm.DateTimeOffset must be ISO 8601 with timezone: {value}'
    elif t == 'odata_edm_boolean':
        if not isinstance(value, bool) and str(value).lower() not in ('true', 'false'):
            return f'Edm.Boolean must be true or false, got: {value}'
    elif t == 'odata_edm_guid':
        if not re.match(r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$', str(value)):
            return f'Edm.Guid must be valid UUID format: {value}'
    elif t == 'odata_edm_binary':
        try:
            base64.b64decode(str(value), validate=True)
        except Exception:
            return f'Edm.Binary must be valid base64-encoded data'

    return None


def validate_field(data: Dict, field_path: str, validations: List[Dict]) -> Dict:
    value, found = get_value_by_path(data, field_path)
    errors = []

    if not found:
        has_presence_check = any(v.get('type') in ('required', 'not_null') for v in validations)
        if has_presence_check:
            errors.append('Field not found in JSON payload')
    else:
        items = value if isinstance(value, list) else [value]
        for idx, item in enumerate(items):
            prefix = f'[{idx}] ' if isinstance(value, list) else ''
            for rule in validations:
                err = validate_single(item, rule)
                if err:
                    errors.append(f'{prefix}{err}')

    return {
        'field_path': field_path,
        'value': value if found else None,
        'found': found,
        'valid': len(errors) == 0,
        'errors': errors,
    }


def validate_group_rule(data: Dict, group: Dict) -> Dict:
    fields = group.get('fields', [])
    rule_type = group.get('rule_type', '')
    name = group.get('name', 'Group Rule')

    present_count = sum(
        1 for f in fields
        if (lambda v, ok: ok and not is_empty(v))(*get_value_by_path(data, f))
    )

    error = None
    if rule_type == 'at_least_one' and present_count == 0:
        error = f'At least one of [{", ".join(fields)}] must have a value'
    elif rule_type == 'exactly_one' and present_count != 1:
        error = f'Exactly one of [{", ".join(fields)}] must have a value (found {present_count})'
    elif rule_type == 'all_or_none' and 0 < present_count < len(fields):
        error = f'Either all or none of [{", ".join(fields)}] must have values'
    elif rule_type == 'mutually_exclusive' and present_count > 1:
        error = f'Only one of [{", ".join(fields)}] may have a value (found {present_count})'

    return {
        'name': name,
        'rule_type': rule_type,
        'fields': fields,
        'valid': error is None,
        'error': error,
    }


def validate_json_against_template(data: Any, template) -> Dict:
    field_results = []
    for rule in sorted(template.rules, key=lambda r: r.order_index):
        result = validate_field(data, rule.field_path, json.loads(rule.validations))
        result['display_name'] = rule.display_name or rule.field_path
        field_results.append(result)

    group_results = []
    for gr in template.group_rules:
        group_results.append(validate_group_rule(data, {
            'name': gr.name,
            'fields': json.loads(gr.fields),
            'rule_type': gr.rule_type,
        }))

    passed = sum(1 for r in field_results if r['valid'])
    g_passed = sum(1 for r in group_results if r['valid'])
    all_valid = (passed == len(field_results)) and (g_passed == len(group_results))

    return {
        'valid': all_valid,
        'template_name': template.name,
        'summary': {
            'total_fields': len(field_results),
            'passed': passed,
            'failed': len(field_results) - passed,
            'group_rules_total': len(group_results),
            'group_rules_passed': g_passed,
            'group_rules_failed': len(group_results) - g_passed,
        },
        'field_results': field_results,
        'group_results': group_results,
    }
