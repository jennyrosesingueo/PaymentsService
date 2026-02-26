# Production Considerations

This document describes the recommended improvements before running the Payments Service in a live,
customer-facing environment. Each item addresses a real risk that becomes important once real money
and real users are involved.

---

## Use Redis for Distributed Idempotency

### What it means
Right now, the service checks for duplicate payments by looking up the reference number in the
database. This works fine when there is only one copy of the service running. In production, however,
multiple copies of the service typically run at the same time to handle more traffic. Two different
copies could receive the same payment request simultaneously, both check the database at the same
instant, both find nothing, and both try to process it — resulting in a double charge before the
database constraint has a chance to catch it.

Redis is a very fast memory store that all copies of the service can check together. The first copy
to receive a request claims the reference number in Redis immediately. Any copy that arrives a
fraction of a second later sees it is already claimed and stops — before ever touching the database.

### Why it matters
- Prevents double charges under high traffic.
- The Redis check is much faster than a database query, so it also speeds up every payment request.
- The database unique index remains as a final safety net, but Redis stops the problem earlier and
  more efficiently.

### What needs to change
- Add a Redis connection to the service configuration.
- Before processing any payment, check Redis for the reference number first.
- If found, return the stored result immediately. If not found, record it in Redis and continue.

---

## Implement the Outbox Pattern

### What it means
When a payment is saved, the service may also need to notify other systems — for example, sending a
confirmation email, updating an accounting system, or triggering a fulfilment workflow. The simplest
approach is to save the payment and then send the notification in the same step. The problem is: if
the notification fails halfway through, you end up with a saved payment but no notification sent —
and no record that the notification was missed.

The outbox pattern solves this by saving both the payment and a "message to be sent" together in a
single step. A separate background process then reads those pending messages and sends them. If the
send fails, the message stays in the outbox and the background process tries again. Nothing is lost.

### Why it matters
- Guarantees that every payment triggers its downstream notification — no silent failures.
- The payment is saved and the notification is queued as one atomic action; they either both succeed
  or both are rolled back.
- The background sender can retry failed deliveries automatically without any manual intervention.

### What needs to change
- Add an outbox table to the database to hold pending messages alongside payment records.
- When saving a payment, write the outbox message in the same database transaction.
- Add a background worker that reads unsent messages, delivers them, and marks them as sent.

---

## Add Monitoring and Logging

### What it means
In development, if something goes wrong you can look at the output on your screen. In production,
the service runs on a server you cannot watch directly, processes thousands of requests, and may
fail in ways that are hard to notice. Monitoring and logging means the service continuously records
what it is doing and alerts you when something goes wrong — before customers notice.

**Logging** records a written history of every significant event: a payment was received, a payment
was rejected, an error occurred. These logs are stored centrally so you can search them later.

**Monitoring** watches live numbers — how many payments are being processed per minute, how long
each one takes, how many are failing — and raises an alert if anything looks abnormal.

### Why it matters
- You can find the cause of a problem quickly by searching the logs rather than guessing.
- Alerts notify you of issues (a spike in rejections, a slowdown) before they become outages.
- Regulators and auditors often require a full audit trail of every payment transaction.
- You can spot patterns — for example, a particular currency being rejected more than expected.

### What needs to change
- Connect the service to a centralised logging platform (for example, Azure Monitor, Datadog, or
  the ELK stack).
- Add structured log entries at key points: payment received, payment outcome, errors.
- Define dashboards that show payment volume, success rate, and response times.
- Set up alerts for error rate thresholds and unusual patterns.

---

## Enable Rate Limiting

### What it means
Rate limiting controls how many requests a single caller is allowed to make within a given time
window — for example, no more than 100 payments per minute from the same account. Without it, a
single misbehaving client (or an attacker) can flood the service with thousands of requests,
slowing it down or taking it offline for everyone else.

### Why it matters
- Protects the service from being overwhelmed by a sudden burst of requests, accidental or deliberate.
- Prevents one customer from consuming all the available capacity, keeping the service fair and
  available for everyone.
- Reduces the impact of certain types of attack where an attacker tries to guess valid reference
  numbers or credentials by making millions of attempts.
- Keeps infrastructure costs predictable — without limits, a single rogue client could drive up
  server and database costs significantly.

### What needs to change
- Define sensible limits per client — for example, 100 requests per minute for standard clients.
- Return a `429 Too Many Requests` response when a client exceeds their limit, with a message
  telling them when they can try again.
- Consider stricter limits on the login endpoint specifically, to slow down any attempt to guess
  credentials.
- If Redis is already in use for idempotency (see above), it can also track request counts
  efficiently across multiple service instances.
