# ── Stage 1: Build ───────────────────────────────────────────────────────────
# Node 20 Alpine — small base, enough for pnpm + Vite build.
FROM node:22-alpine AS builder

# corepack ships with Node 20 and manages pnpm versions without a global install.
RUN corepack enable && corepack prepare pnpm@latest --activate

WORKDIR /app

# Copy lockfile + manifest first so Docker caches the install layer.
# pnpm install only re-runs when these two files change, not on every source edit.
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile --config.dangerouslyAllowAllBuilds=true

# Copy the rest of the source after deps are cached.
COPY . .

# VITE_* variables are embedded into the JS bundle at build time by Vite.
# They are NOT available at container runtime — they must come in as build args.
# docker-compose passes these via the build.args section.
ARG VITE_API_URL
ARG VITE_CLERK_PUBLISHABLE_KEY
ENV VITE_API_URL=${VITE_API_URL}
ENV VITE_CLERK_PUBLISHABLE_KEY=${VITE_CLERK_PUBLISHABLE_KEY}

RUN pnpm build
# Output lands in /app/dist — a flat directory of static HTML/JS/CSS.

# ── Stage 2: Serve ────────────────────────────────────────────────────────────
# nginx Alpine — ~5 MB image, purpose-built for serving static files.
# The Node SDK from Stage 1 is discarded; final image has no build tools.
FROM nginx:alpine AS final

# Remove the default nginx site config that ships with the image.
RUN rm /etc/nginx/conf.d/default.conf

# Our config handles React Router (all paths → index.html) and asset caching.
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Pull the built React app from the builder stage.
COPY --from=builder /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
