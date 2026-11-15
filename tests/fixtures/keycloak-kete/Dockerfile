# syntax=docker/dockerfile:1
# Disposable evaluation only: normal Aspire/KIND/Testcontainers image does not include KETE.
FROM localhost/teck/keycloak:26.8.2-theme-local
ADD --chown=1000:0 --checksum=sha256:10a817cba3c6c67b35d1625fc5fd31ce6aade982e81fc175ba108f36cdef06ce https://github.com/FortuneN/kete/releases/download/2026.08.27.08.03/kete.jar /opt/keycloak/providers/kete.jar
RUN /opt/keycloak/bin/kc.sh build --db=postgres --health-enabled=true --metrics-enabled=true
ENV kete.enabled=false
USER 1000
ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--optimized"]
