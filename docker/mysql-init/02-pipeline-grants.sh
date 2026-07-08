#!/bin/sh
set -eu

MYSQL_USER_VALUE="${MYSQL_USER:-wordpress}"

mysql --protocol=socket -uroot -p"${MYSQL_ROOT_PASSWORD}" <<SQL
GRANT ALL PRIVILEGES ON \`pipeline\`.* TO '${MYSQL_USER_VALUE}'@'%';
FLUSH PRIVILEGES;
SQL
