# Serves the WebGL build (built into docs/ with the "Web - playnesc" build profile) on Coolify.
FROM nginx:1.27-alpine
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY docs/ /usr/share/nginx/html/
EXPOSE 80
