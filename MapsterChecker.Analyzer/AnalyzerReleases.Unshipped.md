# Release 1.0.1

## New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MAPSTER001 | MapsterChecker | Warning | Detects nullable to non-nullable type mappings
MAPSTER001P | MapsterChecker | Warning | Detects nullable to non-nullable property mappings
MAPSTER002 | MapsterChecker | Warning | Detects incompatible type mappings
MAPSTER002P | MapsterChecker | Warning | Detects incompatible property type mappings
MAPSTER003 | MapsterChecker | Info | Detects missing property mappings
MAPSTER003P | MapsterChecker | Info | Detects missing property mappings (property-level)
MAPSTER004 | MapsterChecker | Warning | Detects dangerous expressions in custom mappings
MAPSTER005 | MapsterChecker | Error | Detects custom mapping return type incompatibility
MAPSTER006 | MapsterChecker | Warning | Detects custom mapping null value issues