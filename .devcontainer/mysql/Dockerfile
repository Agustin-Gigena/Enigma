FROM mysql:8.0

ENV MYSQL_ROOT_PASSWORD=root_password \
    MYSQL_USER=enigma \
    MYSQL_PASSWORD=enigma_dev_password \
    MYSQL_DATABASE=enigma_db

# MySQL 8.0 auto-runs *.sh/*.sql under /docker-entrypoint-initdb.d/ on first start only.
COPY seed/ /docker-entrypoint-initdb.d/

EXPOSE 3306
