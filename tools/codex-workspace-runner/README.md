# Codex Workspace Runner

`codex-workspace-runner.ps1`는 같은 폴더의 `codex-runner-input.md`를 작업 요청으로 사용하고, 실행 결과를 워크스페이스의 `temp/codex-runner-cache` 아래에 저장합니다.

## 실행 명령

```powershell
.\tools\codex-workspace-runner\codex-workspace-runner.ps1
```

기본 실행은 토큰 사용량을 줄이기 위해 임시 폴더에서 Codex를 시작합니다. 저장소 지침까지 포함한 전체 작업이 필요할 때만 다음 옵션을 사용합니다.

```powershell
.\tools\codex-workspace-runner\codex-workspace-runner.ps1 -LoadRepositoryInstructions
```

## 결과 폴더

실행 결과는 날짜별 폴더에 저장됩니다.

```text
temp/codex-runner-cache/yyyy-MM-dd/
```

예를 들어 2026년 6월 6일 21:42:34.986에 실행했다면 다음과 같은 파일이 만들어집니다.

```text
temp/codex-runner-cache/2026-06-06/214234-986_input.md
temp/codex-runner-cache/2026-06-06/214234-986_output.md
temp/codex-runner-cache/2026-06-06/214234-986.jsonl
temp/codex-runner-cache/2026-06-06/214234-986.summary.json
temp/codex-runner-cache/2026-06-06/214234-986.stderr.log
```

## 파일별 의미

| 파일 | 의미 | 언제 확인하면 좋은가 |
| --- | --- | --- |
| `{시간}_input.md` | 실행 당시 `codex-runner-input.md`의 복사본입니다. | 어떤 요청으로 실행했는지 나중에 확인할 때 봅니다. |
| `{시간}_output.md` | Codex의 마지막 최종 답변만 추출한 파일입니다. | 사람이 읽을 최종 결과가 필요할 때 가장 먼저 봅니다. |
| `{시간}.jsonl` | `codex exec --json`이 출력한 원본 JSONL 이벤트 스트림입니다. | Codex가 실제로 제공한 원본 응답, 중간 이벤트, usage 이벤트를 확인할 때 봅니다. |
| `{시간}.summary.json` | runner가 원본 JSONL에서 사용량과 실행 정보를 요약한 파일입니다. | 토큰 사용량, 모델, reasoning 설정, 실행 성공 여부를 빠르게 확인할 때 봅니다. |
| `{시간}.stderr.log` | Codex 실행 중 stderr로 출력된 로그입니다. | 실행이 실패했거나 경고 원인을 확인할 때 봅니다. 정상 실행이면 비어 있을 수 있습니다. |

## 무엇을 먼저 보면 되나

일반적으로는 다음 순서로 확인하면 됩니다.

1. `{시간}_output.md`: 최종 답변 확인
2. `{시간}.summary.json`: 토큰 사용량과 실행 설정 확인
3. `{시간}.stderr.log`: 오류가 있었는지 확인
4. `{시간}.jsonl`: 원본 이벤트를 세부 분석해야 할 때 확인
5. `{시간}_input.md`: 실행 당시 요청 내용을 재확인

## 토큰 사용량 읽는 법

`{시간}.summary.json`의 `usage` 항목을 확인합니다.

```json
{
  "usage": {
    "input_tokens": 48930,
    "cached_input_tokens": 44800,
    "output_tokens": 221,
    "reasoning_output_tokens": 70
  }
}
```

- `input_tokens`: 모델에 들어간 전체 입력 토큰입니다.
- `cached_input_tokens`: `input_tokens` 중 서버 캐시가 재사용된 토큰입니다.
- `output_tokens`: 최종 답변과 도구 이벤트 등에 사용된 출력 토큰입니다.
- `reasoning_output_tokens`: 출력 토큰 중 reasoning에 사용된 토큰입니다.

실제 새로 처리된 입력 토큰은 대략 다음처럼 보면 됩니다.

```text
uncached_input = input_tokens - cached_input_tokens
```

토큰 절감이 잘 되었는지 볼 때는 `cached_input_tokens`보다 `input_tokens` 자체가 낮아졌는지를 먼저 확인하는 것이 좋습니다.
