# AI agent 토큰 절약법 찾기

> ai agent에게 작업을 요청하면 ai agent가 문서를 읽거나 작업중에 발생하는 로그 등을 읽고, 결과를 출력하면서 토큰이 계속해서 소비되잖아? 이 토큰 소비량을 가능한한 최소화 하는 방법에 대한 내용을 찾아서 정리해줄래?

<details><summary>2 previous messages</summary>

> 관련 내용은 제품별로 계속 바뀌는 부분이 있어서 공식 문서 위주로 확인해 보겠습니다. 토큰 자체를 줄이는 방법과, 비용만 줄이는 캐싱/모델 선택을 분리해서 정리하겠습니다.
>
> <details><summary>Searched web 7 times</summary>
>
> <details><summary>Searched web 7 times</summary>
>
> - Searched web for `site:platform.openai.com/docs prompt caching OpenAI cached input tokens`
> - Searched web for `Anthropic docs prompt caching token efficient tool use context editing token-efficient tool use`
> - Searched web for `site:docs.anthropic.com/en/docs/build-with-claude prompt caching`
> - Searched web for `docs.anthropic.com prompt caching ephemeral cache_control 5 minutes 1 hour`
> - Searched web for `https://platform.openai.com/docs/guides/latency-optimization`
> - Searched web for `site:platform.openai.com/docs reasoning effort tokens OpenAI API reasoning models`
> - Searched web for `https://platform.openai.com/docs/guides/reasoning?lang=javascript`
>
> </details>
>
> </details>
</details>

조사 기준: 2026년 6월 6일 현재 공식 문서 위주로 정리했습니다.

**핵심 결론**
토큰을 줄이는 가장 큰 방법은 “모델에게 보여주는 입력을 줄이는 것”입니다. 캐싱은 비용/지연시간을 줄이는 데 매우 효과적이지만, 프롬프트 자체가 짧아지는 것은 아닙니다.

**우선순위별 방법**

1. **전체 문서/전체 로그를 넣지 말고, 먼저 범위를 좁히기**
   - 파일 전체 대신 관련 파일명, 함수명, 에러 주변 라인만 읽게 지시.
   - 로그는 `tail`, 에러 패턴 검색, 에러 전후 N줄만 확인.
   - HTML, JSON, 빌드 로그처럼 잡음이 많은 입력은 정제 후 전달.
   - OpenAI도 RAG 결과 pruning, HTML cleaning 같은 context filtering을 권장합니다.
   Source: [OpenAI latency optimization](https://developers.openai.com/api/docs/guides/latency-optimization)

2. **중간 출력과 명령 결과를 줄이기**
   - “명령 출력 전체를 붙이지 말고 핵심 줄만 요약해라”라고 요청.
   - 대량 출력은 파일에 저장하고, 에이전트가 필요한 패턴만 검색하게 하기.
   - 최종 답변도 “변경사항, 검증 결과, 남은 이슈만 10줄 이내”처럼 제한.

3. **대화/작업 기록을 주기적으로 압축하기**
   - 긴 세션에서는 이전 대화 전체가 계속 입력으로 붙어 토큰이 커집니다.
   - 오래된 메시지는 삭제, trim, 요약으로 교체.
   - LangChain은 trim/delete/summarize를 대표 패턴으로 제시하고, OpenAI Agents SDK도 tool result dedupe, context trimming, compaction hook을 제공합니다.
   Sources: [LangChain short-term memory](https://docs.langchain.com/oss/javascript/langchain/short-term-memory), [OpenAI Agents SDK sessions](https://openai.github.io/openai-agents-js/guides/sessions/)

4. **도구/MCP/플러그인을 필요한 것만 켜기**
   - 도구 설명, schema, tool result도 입력 토큰이 됩니다.
   - Anthropic 문서도 tool 요청 비용에 `tools` 파라미터, tool_use, tool_result 블록이 포함된다고 설명합니다.
   - 많은 도구를 항상 노출하지 말고, 작업별로 필요한 도구만 활성화하는 게 좋습니다.
   Source: [Claude tool use pricing](https://platform.claude.com/docs/ko/agents-and-tools/tool-use/overview)

5. **프롬프트 캐싱을 활용하기**
   - 반복되는 긴 system prompt, 규칙, 예시, 도구 정의는 앞쪽에 두고, 매번 바뀌는 사용자 입력은 뒤쪽에 둡니다.
   - OpenAI prompt caching은 1,024토큰 이상에서 자동 적용되며, static prefix가 같을수록 효과가 큽니다.
   - OpenAI 문서상 input token 비용을 최대 90%, latency를 최대 80% 줄일 수 있습니다.
   - Anthropic은 `cache_control` 기반이며 기본 5분 TTL, 추가 비용으로 1시간 TTL 옵션이 있습니다.
   Sources: [OpenAI prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching), [Claude prompt caching](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)

6. **출력/추론 예산 제한하기**
   - API에서는 `max_output_tokens`, 낮은 `text.verbosity`, 낮은 `reasoning.effort`를 검토.
   - OpenAI reasoning 모델은 낮은 effort가 속도와 경제적인 토큰 사용에 유리하다고 설명합니다.
   - 단, 너무 낮추면 품질이 떨어질 수 있으니 단순 작업부터 적용하는 게 안전합니다.
   Source: [OpenAI reasoning models](https://developers.openai.com/api/docs/guides/reasoning)

7. **측정부터 하기**
   - 매 요청의 input/output/cached/reasoning token을 기록해야 어디서 새는지 보입니다.
   - OpenAI는 `usage` 필드와 `cached_tokens`로 사용량을 확인할 수 있다고 안내합니다.
   Source: [OpenAI managing tokens](https://developers.openai.com/api/docs/guides/advanced-usage)

**에이전트에게 바로 붙여 쓸 지시문**

```text
토큰 절약 모드로 작업해줘.
- 먼저 rg/검색으로 후보를 좁히고, 필요한 파일/라인만 읽어.
- 전체 문서/전체 로그를 출력하지 말고 에러 주변과 핵심 줄만 확인해.
- 긴 명령 출력은 요약하고, 원문 반복 출력은 피하라.
- 중간 보고는 1~2문장으로 제한해.
- 컨텍스트가 길어지면 지금까지의 핵심 결론/파일/결정사항만 요약해서 이어가.
- 최종 답변은 변경사항, 검증 결과, 남은 이슈만 간단히 정리해.
```

가장 실전적인 조합은 `범위 좁히기 + 대용량 출력 억제 + 주기적 compact + 필요한 도구만 켜기 + prompt caching`입니다.