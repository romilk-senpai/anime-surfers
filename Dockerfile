# Serves the WebGL build (built into Build/ with the "Web - playnesc" build profile) on Coolify.
FROM nginx:1.27-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY Build/ /usr/share/nginx/html/
EXPOSE 80
