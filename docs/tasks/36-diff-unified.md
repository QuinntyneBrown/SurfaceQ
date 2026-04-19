# 36 — `diff` differs → exit 1 + unified diff

**Traces to:** L2-003

## Goal
When files differ, `diff` prints a unified diff and exits `1`.

## Failing test
Modify `public-api.ts` (insert a line). Run `diff`. Assert exit `1`, stdout starts with `---` / `+++` headers and contains `+<new line>`.

## Implementation
Implement a small unified-diff formatter (LCS or line-by-line) — keep it plain and test-covered. One public function `string UnifiedDiff(string expected, string actual, string label)`.

## Done when
- Test green.
