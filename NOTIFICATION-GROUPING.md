# Notification Grouping

Как Preesta превращает поток tickets в дискретные сообщения и почему так.

## Двухуровневая группировка

Чтобы не спамить получателю одинаковыми тикетами из разных правил, группировка
проходит в два этапа на двух разных уровнях.

### Уровень 1: Supplier (issues → packages)

`JqlSupplier` / `ReleaseSupplier` группируют issues по ключу
**`{To, Cc, Subject, Rule}`** в [`IssueSupplier.GetPackages`](Preesta/Data/Supplying/IssueSupplier.cs).

Маркеры адресатов (`assignee` / `reporter` / `creator`) разрешаются здесь же —
[`ReplaceMarkersByRealAddresses`](Preesta/Data/Supplying/IssueSupplier.cs#L27)
swap-ит их на реальные email из `Issue.Participants`. Группировка идёт уже по
**resolved** адресам, поэтому два issue с разными `assignee` попадают в **разные
packages** даже если rule один — каждый получатель видит только свои.

```
Rule A (mailTo: assignee, subject: "Daily")
  Issue I1 (assignee=alice@x)  →  Package(to=alice@x, subj=Daily, [I1])
  Issue I2 (assignee=bob@x)    →  Package(to=bob@x,   subj=Daily, [I2])

Rule B (mailTo: alice@x, subject: "Daily")
  Issue I3                      →  Package(to=alice@x, subj=Daily, [I3], rule=B)
```

`Rule` входит в ключ группировки специально: разные правила могут указывать
**одинаковый** subject, но иметь разные `Recommendations`, `TelegramChatIds`,
`Columns`. Без `Rule` в ключе эти per-rule поля брались бы из `ag.First().rule`
и мы бы теряли их для всех остальных правил в группе. Это была реальная проблема
до Phase 7 ("TelegramChatIds bug"); fix — добавить `Rule` в ключ.

### Уровень 2: MessageBuilder (packages → messages)

[`MessageBuilder<T>.ToMessage`](Preesta/Data/Supplying/Convert/MessageBuilder.cs)
группирует packages по ключу **`{To, Cc, Subject}`** (без `Rule` — здесь rule
уже неважен, мы делаем финальную почту). Один Message содержит все packages,
у которых совпали адресаты и тема. `IssueFormatter` отрисует их как **отдельные
секции** в теле — у каждой свои recommendations, JQL-link, columns.

В итоге пользователь Alice из примера выше получит **один** email с двумя
секциями: одна от Rule A (issue I1), другая от Rule B (issue I3). Если subject
у Rule A и Rule B был бы разный — пришло бы два разных email-а.

## Telegram: независимая группировка

[`MessageBuilder<T>.ToTelegramMessages`](Preesta/Data/Supplying/Convert/MessageBuilder.cs)
работает по другому ключу — **`chatId`**. На один `chatId` идёт одно сообщение
со всеми packages, которые на этот chat указаны (через `TelegramChatIds` или
через `telegramUsers` map с резолвом email→chatId). Email-группировка и
Telegram-группировка независимы — у каналов разная природа адресации
(emails объединяются в To/Cc одного письма; Telegram chats не "комбинируются",
каждый получает своё сообщение).

`telegramUsers` mapping (config) переиспользует тот же `Redirector`, что и
email — расширяя такие маркеры как `managers` в список emails, и потом мапя
каждый email на personal `chatId`. См. [`Redirector.ResolveRecipients`](Preesta/Notification/Redirector.cs).

## Тестовое покрытие

| Тест | Что проверяет |
|---|---|
| [`GroupingTests.GroupReleases`](Tests/GroupingTests.cs) | 3 ReleaseRules → 2 messages: rules с одинаковым subject склеиваются, разный subject — отдельный message. Порядок адресатов нормализуется (OrderBy). |
| [`GroupingTests.GroupIssues`](Tests/GroupingTests.cs) | 3 JqlRules с одинаковым subject → 3 packages в Supplier (по-rule), 1 message в MessageBuilder (по {To,Cc,Subject}). |
| [`PerIssueSplittingTests.AssigneeMarkerSplitsIssuesByAssigneeIntoSeparatePackages`](Tests/PerIssueSplittingTests.cs) | 3 issues с разными assignees + один rule с `mailTo: assignee` → 2 packages, каждый получатель видит только свои issues. |
| [`PerIssueSplittingTests.ReporterMarkerInCcSplitsByReporter`](Tests/PerIssueSplittingTests.cs) | то же для reporter в Cc. |
| [`TelegramTests.TelegramMessagesCreatedForRulesWithChatId`](Tests/TelegramTests.cs) | Rule с статичным `telegramChatId` → 1 Telegram-сообщение. |
| [`TelegramTests.RedirectionRulesExpandToMultipleTelegramChatIds`](Tests/TelegramTests.cs) | `mailTo: managers` + redirectionRules `managers → 3 emails` + telegramUsers map → 3 Telegram-сообщения. |
| [`TelegramTests.AssigneeMarkerResolvesToTelegramChatId`](Tests/TelegramTests.cs) | `mailTo: assignee` + telegramUsers `{assignee_email: chatId}` → 1 Telegram-сообщение на верный chatId. |

## Что бы поменял в будущем

Текущий `NotificationSpec` держит email-поля и Telegram-поля плоско рядом:
```
NotificationSpec
├── Subject
├── RawRecipients (email To)        ← ключ группировки + resolved через Redirector
├── RawCc          (email Cc)        ← то же
├── Recommendations
├── TelegramChatIds                  ← независимая группировка по chatId
└── Columns                          ← per-section meta-line
```

С добавлением Slack/Teams (Phase 10/11) плоская структура упрётся в потолок.
Логичнее перейти к **channels-based config** — `notify.channels: [...]` где
каждый channel описывает свой адрес и render-стратегию. Это запланировано как
часть Phase 10 (Slack), когда появится третий канал и pattern станет видимым.
