# Notification Grouping: как работает и что нужно менять

## Текущая архитектура

### Двухуровневая группировка

Группировка происходит в два этапа, на двух разных уровнях.

**Уровень 1: Supplier** (JqlSupplier / BuildSupplier) — группировка issues в packages.

Ключ группировки: `{To, Cc, Subject}`.

```
Rule A (JQL: "...", mailTo: team1@x, team2@x, subject: "Alert")  → issue-1, issue-2
Rule B (JQL: "...", mailTo: team2@x, team1@x, subject: "Alert")  → issue-3
Rule C (JQL: "...", mailTo: team1@x, team2@x, subject: "Other")  → issue-4
```

Результат:
- Package 1: subject="Alert", to=team1@x,team2@x → [issue-1, issue-2, issue-3]
- Package 2: subject="Other", to=team1@x,team2@x → [issue-4]

Для Issues адреса `assignee`/`reporter`/`creator` заменяются на реальные email (per-issue),
поэтому группировка идёт по **resolved** адресам, а не маркерам. Это значит,
что issues с разными assignee попадут в разные packages, даже если rule один.

Для Builds маркеры не используются — только статические адреса.

**Уровень 2: Converter** (Common.ToMessage) — группировка packages в messages.

Тот же ключ: `{To, Cc, Subject}`. Packages с одинаковыми адресатами и темой
склеиваются в одно письмо. На практике это мержит packages от разных suppliers
(JQL + Structure), если адресаты совпадают.

### Telegram: отдельная группировка

`Common.ToTelegramMessages` группирует по `chatId`:
- Собирает все packages, у которых есть `TelegramChatIds`
- Для каждого уникального chatId находит все packages, содержащие этот chatId
- Генерирует одно сообщение на chatId

### Тестовое покрытие

| Тест | Что проверяет |
|------|---------------|
| `GroupingTests.GroupBuilds` | 3 BuildRules → 2 packages. Rules 1 и 2 (одинаковые адресаты + subject "Subject") объединяются, Rule 3 ("DifferentSubject") — отдельно. Порядок адресатов не влияет (OrderBy). |
| `GroupingTests.GroupIssues` | 3 JqlRules → 2 packages. Та же логика: правила с одинаковыми {To, Cc, Subject} объединяются. |
| `TelegramTests.TelegramMessagesCreatedForRulesWithChatId` | Rule с chatId → 1 Telegram-сообщение. |
| `TelegramTests.NoTelegramMessagesWhenNoChatId` | Rule без chatId → 0 Telegram-сообщений. |
| `TelegramTests.ReactionPipeSendsTelegramMessages` | ReactionPipe вызывает оба мессенджера (email + Telegram). |

### Проблема текущего дизайна

`TelegramChatIds` прибит к `Notify`/`SendsNotification` — тому же объекту, где живут email-адреса.
Группировка в Supplier идёт по `{To, Cc, Subject}`, а `TelegramChatIds` берётся из
`ag.First().rule` — т.е. из первого rule в группе, что может быть некорректно, если у
разных rules в группе разные chatIds.

```
Notify
├── Subject
├── MetaAddressers (email To)      ← ключ группировки
├── MetaCarbonCopy (email Cc)      ← ключ группировки
├── Recommendations
└── TelegramChatIds                ← НЕ участвует в группировке, берётся от первого rule
```

## Предлагаемый рефакторинг

Разделить каналы доставки в конфигурации:

```yaml
rules:
  - type: jql
    jql: "..."
    notify:
      subject: Alert
      recommendations: Fix it
      channels:
        - type: email
          to: assignee
          cc: reporter,managers
        - type: telegram
          chatId: "-1001234567890"
        - type: teams
          webhookUrl: "https://..."
```

Это потребует:
1. Новая модель конфига: `Notify.Channels[]` вместо плоских полей
2. Группировка email и Telegram/Teams должна происходить независимо
3. `SendsNotification` должен содержать список каналов, а не плоские поля
4. `TelegramChatIds` должен участвовать в ключе группировки Telegram-пакетов на уровне Supplier
5. Обновление парсеров (XML, YAML)
6. Обновление всех тестов группировки
