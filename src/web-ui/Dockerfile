FROM node:22-alpine AS build

WORKDIR /app

RUN corepack enable

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .

RUN pnpm build

FROM node:22-alpine

WORKDIR /app

COPY --from=build /app/.output .output
# @nuxt/icon's `local` server bundle mode bakes absolute node_modules paths
# (e.g. .../node_modules/@iconify-json/lucide/icons.json) into the built
# server output instead of inlining the JSON — those icon collections must
# still be on disk at runtime, at the same path, or every icon 404s server-side.
COPY --from=build /app/node_modules ./node_modules

EXPOSE 3000

CMD ["node", ".output/server/index.mjs"]
