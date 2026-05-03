#!/bin/bash
# Azure App Service startup script for ARK JSON Validator
# Gunicorn binds to port 8000 which Azure's reverse proxy forwards to.

gunicorn \
  --bind=0.0.0.0:8000 \
  --timeout=600 \
  --workers=2 \
  --access-logfile=- \
  --error-logfile=- \
  app:app
