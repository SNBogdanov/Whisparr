---
name: git
description: "Git Whisparr: единственная ветка main, remote origin, правила коммита и пуша форка. Use when committing, pushing, rebasing or inspecting Whisparr git history."
---

# Git — Whisparr (форк SNBogdanov)

Репозиторий: `C:\Users\SERGEYBO\Documents\Projects\Whisparr`.

## Remotes и ветка

- `origin` = https://github.com/SNBogdanov/Whisparr.git (форк).
- Remote `upstream` **намеренно отсутствует**: форк ведётся отдельно от
  `Whisparr/Whisparr`. Не добавляй его без явной просьбы.
- Единственная ветка — `main` (она же default на GitHub). Проверяй через
  `git branch --show-current` и `git remote -v`.
- История форка содержит upstream-коммиты до прежней базы (`c55fdc66a`);
  расхождения описаны в `CHANGELOG.md`.

## Жёсткие правила

- **Никогда** не коммить, не делать amend, push или PR без явной просьбы.
- Коммит и пуш — только субагент `git-commit` (команда `/commit`). Main-сессия
  не выполняет `git add/commit/push` сама.
- Перед коммитом: `git status`, `git diff`, `git log --oneline -10`;
  ветку/upstream — `git branch --show-current`,
  `git rev-parse --abbrev-ref --symbolic-full-name @{u}`.
- Не делать force, amend, `--no-verify`; не менять git config.
- Не коммитить секреты и артефакты: `_output/`, `_temp/`, `node_modules/`,
  `bin/`, `obj/`, логи, ключи/токены.

## Сообщения коммитов

- Следуй стилю последних коммитов (`git log --oneline -10`); кратко, по делу.
- Не добавляй процесс («я забыл») — только суть изменения.

## Push

- Push — только в текущую ветку (`main`) на `origin`, без force. В `upstream`
  ничего не отправляется (remote отсутствует).
- Если push отклонён (non-fast-forward, auth, VPN) — не force: сообщи причину
  и что осталось незакоммиченным.
