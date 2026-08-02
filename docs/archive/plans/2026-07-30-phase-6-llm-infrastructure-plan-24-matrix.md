# Plan 24: Manual validation matrix

**Branch:** `task/phase-6-matrix`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 24

## Steps

### 1. Create validation matrix document
- File: `docs/manual-validation/2026-07-30-phase-6-llm-infrastructure-matrix.md`

### 2. Matrix sections

#### Provider CRUD
| # | Test | Steps | Expected |
|---|---|---|---|
| 1 | Create provider | Admin → Providers → Add. Fill form, save. | Provider appears in list. |
| 2 | Edit provider | Click provider, edit name/URL, save. | Changes persisted. |
| 3 | Disable provider | Disable toggle/button. | Provider greyed out, `IsEnabled = false`. |
| 4 | API key write-only | Edit provider — API key field empty. | Key not displayed back. |
| 5 | Probe models | Click "Probe Models" on OpenAI-compatible provider. | Model list returned (or empty for unconfigured). |

#### Model Config CRUD
| # | Test | Steps | Expected |
|---|---|---|---|
| 6 | Add model config | Provider Models → Add. Fill model ID, name, tier. | Model appears in list. |
| 7 | Edit model config | Edit price/tier/max tokens. | Changes persisted. |
| 8 | Delete model config | Delete with confirmation. | Model removed. |

#### Routing
| # | Test | Steps | Expected |
|---|---|---|---|
| 9 | View routing table | Admin → Routing. | 11 rows, one per AiFeature. |
| 10 | Edit default tier | Change PersonalChat to Premium. | Persisted on reload. |
| 11 | Lock feature | Set MaxUserTier to "Locked". | Users can't override. |

#### Usage Dashboard
| # | Test | Steps | Expected |
|---|---|---|---|
| 12 | View token usage | Admin → Usage → Token tab. | Table loads with filters. |
| 13 | Filter by user | Search user, apply. | Results filtered. |
| 14 | Filter by date | Set from/to dates. | Results filtered. |
| 15 | Pagination | Navigate pages. | Correct page data. |
| 16 | Image usage tab | Switch to Image tab. | Image records displayed. |

#### Account Self-Service
| # | Test | Steps | Expected |
|---|---|---|---|
| 17 | View own usage | User menu → Usage. | Token/image bars + recent calls. |
| 18 | Unlimited budget | Budget = 0. | "Unlimited" displayed. |

#### Budget Enforcement
| # | Test | Steps | Expected |
|---|---|---|---|
| 19 | Budget exceeded | Set low budget, trigger LLM call. | `TOKEN_BUDGET_EXCEEDED` error, 429 status. |
| 20 | Image budget exceeded | Set low image budget, trigger image gen. | `IMAGE_BUDGET_EXCEEDED` error. |

#### AiNarrative Job
| # | Test | Steps | Expected |
|---|---|---|---|
| 21 | Hangfire dashboard | Visit `/hangfire` as admin. | Dashboard loads, recurring job listed. |
| 22 | Trigger job manually | Dashboard → "ai-narrative-gen" → Trigger. | Job executes, narratives populated. |
| 23 | Dry-run with no providers | Trigger with no LLM providers configured. | Job logs warning, no crash. |

#### Encryption
| # | Test | Steps | Expected |
|---|---|---|---|
| 24 | Missing encryption key | Remove `Llm:EncryptionKey`, start server. | Server refuses to start with clear error. |
| 25 | Invalid key | Set key to "not-base64". | Server refuses to start. |
| 26 | Key too short | Set key to base64 of 16 bytes. | Server refuses to start. |

## Verification
- Execute matrix manually after all tasks complete.
- Check off each row.
- File any bugs found as new issues.