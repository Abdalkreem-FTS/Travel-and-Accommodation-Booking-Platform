# Stage 1: build the app with Node. Only the output (dist/) is kept.
FROM node:24-alpine AS build

WORKDIR /app

# Copy the package files first, so Docker can reuse the installed
# node_modules layer when only the source code changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Stage 2: serve the static files with nginx. No Node, no source code.
FROM nginx:alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
