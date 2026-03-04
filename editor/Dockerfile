# ─────────────────────────────────────────────────────────────────────────────
# DocumentGenerator — Badge Template Editor
#
# Simple Node.js + Express service.
# The templates/ directory is injected at runtime via a Docker volume mount.
# ─────────────────────────────────────────────────────────────────────────────

FROM node:20-alpine

# Non-root user for security
RUN addgroup -S editor && adduser -S editor -G editor

WORKDIR /app

# Install production dependencies only
COPY package.json ./
RUN npm install --omit=dev && npm cache clean --force

# Copy application source
COPY server.js  ./
COPY public/    ./public/

# The templates directory is expected to be mounted at /templates
ENV TEMPLATES_DIR=/templates \
    PORT=3500 \
    NODE_ENV=production

# Switch to non-root
USER editor

EXPOSE 3500

HEALTHCHECK --interval=15s --timeout=5s --retries=3 --start-period=5s \
  CMD wget -qO- http://localhost:3500/api/templates || exit 1

CMD ["node", "server.js"]
