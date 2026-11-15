#!/bin/sh
set -eu
mkdir -p /home/app/.pki/nssdb
certutil -N --empty-password -d sql:/home/app/.pki/nssdb
certutil -A -n teck-kind-ca -t 'C,,' -i /certs/ca.crt -d sql:/home/app/.pki/nssdb
exec dotnet /app/Identity.ClusterProbe.dll
