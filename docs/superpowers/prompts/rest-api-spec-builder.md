You are building a REST Web API specification document in markdown. The document
lives at `docs/ibkr-web-api-spec.md`. You will receive chunks of HTML source data one at a time.
For each chunk, extract the API information and append it to the document following
the structure below exactly.

## Document Structure

The top-level heading (#) is the API title. Below it are **categories** (## headings),
each with a description. Inside each category are **endpoints** (### headings).

## Category Format

```markdown
## Category Name

Category description goes here.
```

## Endpoint Format

```markdown
### Endpoint Name

Description of what this endpoint does.

- **Method:** `GET` | `POST` | `PUT` | `DELETE` | `PATCH`
- **URL:** `/v1/api/resource/{pathParam}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| pathParam | path | string | yes | Description |
| filter | query | string | no | Description |

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | Description |
| config | object | no | See nested table below |

**`config` object:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| key | string | yes | Description |

#### Example Request

```json
{
  "name": "my-resource",
  "config": {
    "key": "value"
  }
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| id | integer | Description |
| items | array\<Item\> | See nested table below |

**`Item` object:**

| Field | Type | Description |
|-------|------|-------------|
| name | string | Description |

#### Example Response

```json
{
  "id": 123,
  "items": [
    {
      "name": "item-1"
    }
  ]
}
```
```

## Rules

1. **Read the existing file first** before every edit to know where to append.
2. **Append only** — never modify previously approved sections unless explicitly asked.
3. **Omit empty sections** — if an endpoint has no request body, skip the Request Body
   table and Example Request block. If it has no response body, skip the Response Body
   table and Example Response block. If it has no parameters, skip the Parameters table.
4. **Nested objects** — represent as a separate labeled table below the parent table,
   with the parent field's type referencing the nested name (e.g., `object` or
   `array<TypeName>`). Nest to arbitrary depth this way.
5. **Arrays** — use `array<type>` notation (e.g., `array<string>`, `array<Order>`).
6. **Enums** — list allowed values in the description (e.g., "One of: `BUY`, `SELL`").
7. **Required flag** — use `yes` or `no`. If the source is ambiguous, mark it `no`
   and add "(assumed optional)" to the description so it can be reviewed.
8. **Preserve source naming** — use the exact field/parameter names from the HTML.
   Do not rename, camelCase-convert, or normalize them.
9. **One chunk at a time** — after appending, show me only the new section you added
   (not the full file) so I can review it efficiently.
10. **Wait for approval** — do not ask for or process the next chunk until I confirm
    the current section is correct.
11. **Corrections** — when I request changes, edit only the specific section in
    question, then show the corrected version.
12. **Category deduplication** — if the chunk belongs to an existing category, append
    the new endpoints under that category heading rather than creating a duplicate.
13. **Example JSON blocks** — when the source HTML includes example request or response
    JSON, include them as fenced `json` code blocks. If the source does not provide
    examples, construct a representative example from the schema tables using realistic
    placeholder values (not "string" or "0"). Place Example Request after the Request
    Body table and Example Response after the Response Body table.

## Workflow

```
loop:
  1. I paste an HTML chunk
  2. You read the current file, extract the data, append to the document
  3. You show me just the newly added markdown
  4. I review and either approve or request corrections
  5. On approval, I provide the next chunk → go to 1
```

Ready. Paste the first HTML chunk.
