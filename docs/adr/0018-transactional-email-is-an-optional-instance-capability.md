# Transactional Email Is an Optional Instance Capability

planaffe may send transactional email through an SMTP account configured by the
operator. Invitations, password recovery and email-address confirmation use it;
general event notifications do not belong to the MVP. SMTP is optional: an
instance without it still supports bootstrap, existing browser sign-ins, the API
and the CLI.

The earlier MVP boundary excluded email entirely. That kept the deployment to
the application and Postgres, but it left every invitation link for an
administrator to carry through some unrelated channel and left password recovery
without a credible path. Once the browser becomes the normal human interface,
identity cannot be complete while the operator is also the mail delivery
mechanism.

SMTP is configuration, not another planaffe service. The operator supplies a
host, port, transport-security mode, credentials, sender and public base URL.
The application has a narrow email port and owns separate text and HTML
templates; the domain does not know SMTP. A delivery failure is returned and
logged so an administrator can retry. There is no durable queue, retry engine,
subscription model or third production container.

## Consequences

**A solo instance still starts without SMTP.** The bootstrap user token can be
exchanged once for a password and browser session, so the first browser sign-in
does not depend on mail. Actions that necessarily send mail explain that SMTP is
not configured; they do not make the rest of the instance unhealthy.

**Links require one canonical public base URL.** Invitation, recovery and
email-change links are never assembled from an inbound `Host` header. The
operator names the externally reachable origin explicitly.

**One-time secrets are stored only as hashes.** An invitation lasts seven days;
recovery and email-change links last one hour. Reissuing one invalidates the
previous one of the same purpose.
Delivery failure does not make a secret recoverable; retry issues a new one.

**Tests use Mailpit as test infrastructure.** Integration tests inspect the
delivered message through Mailpit's API. Mailpit may join the development Compose
file, but never the production topology.

**The pipeline deliberately stops short of notifications.** Building templates
and an email port makes later notifications cheaper, but no issue event sends
mail in the MVP. “Transactional email” means identity transactions only.
