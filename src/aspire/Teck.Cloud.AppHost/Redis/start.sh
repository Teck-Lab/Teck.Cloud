set -eu
# Alphanumeric generated credentials avoid ambiguous Redis configuration quoting.
case "$REDIS_PASSWORD" in *[!a-zA-Z0-9]*|'') echo 'Redis password must be alphanumeric.' >&2; exit 1;; esac
case "$REDIS_ADVERTISED_HOST" in *[!a-zA-Z0-9.-]*|'') echo 'Redis advertised host must be an IPv4 address or DNS hostname.' >&2; exit 1;; esac
case "$REDIS_ADVERTISED_PORT" in *[!0-9]*|'') exit 1;; esac
if [ "$REDIS_ROLE" = sentinel ]; then
  printf '%s\n' \
    'bind 0.0.0.0' 'port 26379' 'protected-mode yes' \
    "requirepass $REDIS_PASSWORD" \
    "sentinel monitor teck $REDIS_ADVERTISED_HOST $REDIS_PRIMARY_PORT 2" \
    "sentinel auth-pass teck $REDIS_PASSWORD" \
    "sentinel sentinel-pass $REDIS_PASSWORD" \
    "sentinel announce-ip $REDIS_ADVERTISED_HOST" \
    "sentinel announce-port $REDIS_ADVERTISED_PORT" \
    'sentinel resolve-hostnames yes' 'sentinel announce-hostnames yes' \
    'sentinel down-after-milliseconds teck 3000' \
    'sentinel failover-timeout teck 15000' \
    'sentinel parallel-syncs teck 1' > /tmp/teck-sentinel.conf
  exec redis-server /tmp/teck-sentinel.conf --sentinel
fi
set -- redis-server --bind 0.0.0.0 --port 6379 --protected-mode yes \
  --requirepass "$REDIS_PASSWORD" --masterauth "$REDIS_PASSWORD" \
  --replica-announce-ip "$REDIS_ADVERTISED_HOST" --replica-announce-port "$REDIS_ADVERTISED_PORT" \
  --save '' --appendonly no
if [ "$REDIS_ROLE" = replica ]; then
  set -- "$@" --replicaof "$REDIS_ADVERTISED_HOST" "$REDIS_PRIMARY_PORT"
fi
exec "$@"
