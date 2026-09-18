FROM node:24-alpine AS build
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ .
RUN npx ng build --configuration production

FROM caddy:2-alpine AS runtime

COPY --from=build /app/dist/angular-app/browser /srv
COPY frontend/Caddyfile /etc/caddy/Caddyfile

EXPOSE 80 443
