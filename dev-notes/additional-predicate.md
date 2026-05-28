# `additionalPredicate` — a C# reflection backdoor for what JQL can't express

A rule may carry an optional `additionalPredicate: <CSharpMethodName>` knob.
It runs a named static method in `Preesta.ExtendedFilteringPredicates`
against every issue returned by the rule's filter, dropping issues that
don't pass. It is left out of the public docs on purpose — it requires
editing C# to use, and we don't want to advertise it as a configuration
feature.

## Why it exists

Some questions can't be expressed in JQL at all. The original case was
**"find issues with more than one `fixVersion`"** — a violation of the
in-house process rule that a bug ships in exactly one release. JQL has
no `count(fixVersion) > 1` predicate, no list-cardinality function, and
no way to express "this field has more than N values". The cheapest fix
was a C# escape hatch:

```yaml
- tracker: jira
  group: hourly
  filter: 'project = X AND fixVersion is not EMPTY'
  additionalPredicate: MoreThanOneFixVersion
  notify: { … }
```

JQL handles the wide net; the C# method culls the result set.

Other shipped predicates (see `Preesta/ExtendedFilteringPredicates.cs`):

| Predicate | What it checks |
|---|---|
| `MoreThanOneFixVersion` | issue has 2+ fix versions |
| `DueDateExpiredMoreThan2WorkingDays` | due date is two working days in the past |
| `EstimatesAttachmentIsAbsent` | an internal estimate-approval attachment is missing |

## How it dispatches

`IssueSupplier<TRule>.IsIssueInAccordanceWithPredicate` does a single
reflection lookup:

```csharp
typeof(ExtendedFilteringPredicates)
    .GetMethod(additionalPredicateName, BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, new object?[] { JiraService, issue });
```

The method is expected to be an `internal static bool (IJiraService?, Issue)`.
Returning `true` keeps the issue; `false` drops it. Throwing surfaces as
a rule-conversion error and the issue is dropped along with the rule's
batch — predicates that need Jira (e.g. `EstimatesAttachmentIsAbsent`)
should throw `InvalidOperationException` when `jira is null` rather
than silently degrade.

## How to add one

1. Add an `internal static bool Foo(IJiraService? jira, Issue issue)` in
   `Preesta/ExtendedFilteringPredicates.cs`.
2. Reference it from a rule as `additionalPredicate: Foo`.
3. Add a test next to `Tests/FilteringPredicateTests.cs`.

That's it — no DI registration, no schema change. Rebuild and ship.

## Why it isn't in public docs

Authoring a predicate means recompiling Preesta and shipping a new
container image, so it isn't a "config the running tool" feature in any
meaningful sense — it's a code change with a YAML breadcrumb. Surfacing
that on a "how to write a rule" page would be misleading. Reach for
this only when the answer to *"why can't a normal filter express
this?"* is genuinely "the tracker's query language doesn't have a way".
