# Keycloak with Family OS realm pre-staged for --import-realm
FROM quay.io/keycloak/keycloak:26.0
# Bump to force rebuild when realm JSON changes
ARG REALM_REV=4
COPY realm-export.json /opt/keycloak/data/import/realm-export.json
