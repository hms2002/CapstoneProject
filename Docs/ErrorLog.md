---
status: active
authority: project-log
category: error-log
last_reviewed: 2026-05-08
---

# Error Log

This file records recurring implementation errors, their causes, and prevention rules.

## Template

```md
## YYYY-MM-DD - Short error name

Context:

Cause:

Fix:

Prevention:
```

## Active Entries

## 2026-05-08 - Notion Authentication Failure Was Not Escalated Clearly

Context:
The Slime Queen phase 2 health bar layout depended on a Notion page image, but the Notion connector authentication had expired.

Cause:
Work stopped at "Notion authentication expired" instead of immediately asking the user to re-authenticate Notion and then retrying the page read.

Fix:
The user was given the Korean re-authentication path, Notion access was restored, and the Slime Queen phase 2 HUD work continued.

Prevention:
When a required connector reports token expiration or authentication failure, immediately ask the user to reconnect that connector and retry the read before making layout-sensitive changes.
