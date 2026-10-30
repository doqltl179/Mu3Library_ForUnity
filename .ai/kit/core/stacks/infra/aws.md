---
id: aws
title: AWS
kind: infra
applies_to: []
related: [terraform, github-actions, python, typescript, csharp]
volatility: volatile
reviewed: 2026-10-28
sources: ["https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sso.html", "https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html", "https://docs.aws.amazon.com/IAM/latest/UserGuide/confused-deputy.html", "https://github.com/aws-actions/configure-aws-credentials", "https://docs.aws.amazon.com/lambda/latest/dg/lambda-runtimes.html", "https://docs.aws.amazon.com/lambda/latest/dg/best-practices.html", "https://docs.aws.amazon.com/lambda/latest/dg/invocation-recursion.html", "https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-vs-rest.html", "https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/best-practices.html", "https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-tokens-verifying-a-jwt.html", "https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html", "https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/private-content-restricting-access-to-s3.html", "https://registry.terraform.io/providers/hashicorp/aws/latest/docs/guides/version-6-upgrade", "https://github.com/hashicorp/terraform-provider-aws/blob/main/CHANGELOG.md", "https://developer.hashicorp.com/terraform/language/backend/s3", "https://github.com/aws-actions/configure-aws-credentials/releases", "https://github.blog/changelog/2026-04-23-immutable-subject-claims-for-github-actions-oidc-tokens/", "https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-limits.html", "https://aws.amazon.com/about-aws/whats-new/2026/10/aws-lambda-web-functions/", "https://aws.amazon.com/about-aws/whats-new/2025/11/console-credentials-aws-cli-sdk-authentication/", "https://aws.amazon.com/about-aws/whats-new/2023/04/amazon-s3-security-best-practices-buckets-default/", "https://docs.aws.amazon.com/AmazonS3/latest/userguide/default-s3-c-encryption-setting-faq.html", "https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-sign-in-feature-plans.html", "https://aws.amazon.com/about-aws/whats-new/"]
---

# AWS

## Detect
- The tool that owns the resources: Terraform (`provider "aws"`; follow the `terraform` pack), CDK (`cdk.json`), SAM (`template.yaml` with `Transform: AWS::Serverless-2016-10-31`, `samconfig.toml`), CloudFormation templates, or Serverless Framework (`serverless.yml`). Change resources only through it; console and CLI edits drift.
- Accounts, Regions, and profiles: `AWS_PROFILE` and `AWS_REGION` in scripts and CI, provider and backend `region`, and per-environment account splits.
- CI authentication: `aws-actions/configure-aws-credentials` with `role-to-assume` (OIDC) or stored access keys.
- Functions: handler entry points, `runtime` and `architectures` per function, packaging (zip, container image, layers), and the SDK (`boto3`, `@aws-sdk/*` v3, `AWSSDK.*`).
- API type (HTTP API: `apigatewayv2`; REST API: `api_gateway_rest_api`) and its authorizer; DynamoDB key schema and indexes; Cognito user pool clients and scopes; S3 buckets and the CloudFront distributions in front of them.
- Tag keys, budgets, and log retention already in place.

## Conventions
- Humans sign in through IAM Identity Center (`aws configure sso`, then `aws sso login --profile <name>`); workloads and CI use roles with temporary credentials. Never create, commit, or print long-lived access keys, and keep credentials out of code and committed files.
- Least privilege: one execution role per function; name actions and resource ARNs instead of `*`; when a resource policy grants a service principal access, add `aws:SourceArn` or `aws:SourceAccount` conditions (confused deputy).
- Set the Region explicitly per environment. CloudFront viewer certificates (ACM) and Lambda@Edge functions must be created in `us-east-1`.
- Tag every resource with the project's keys (Terraform: provider `default_tags`); a tag appears in cost reports only after it is activated as a cost allocation tag.
- Set a retention period on every CloudWatch Logs group, including each function's; the default keeps logs forever.
- Read secrets from Secrets Manager or Parameter Store `SecureString` at init and cache them; plain environment variables are readable by anyone who can read the function configuration.
- Lambda: create SDK clients and connections outside the handler and reuse them; keep no per-user data in globals between invocations.
- Make handlers idempotent: async invocations retry twice by default and event sources deliver at least once. Use the project's idempotency mechanism (Powertools for AWS Lambda when present), and give async functions an on-failure destination or DLQ.
- Prefer HTTP APIs unless a REST-only feature is needed (API keys and usage plans, request validation, WAF, caching, private or edge-optimized endpoints).
- Authorize APIs with Cognito access tokens and required scopes; ID tokens carry identity claims for the client. Verify tokens against the user pool's JWKS with a maintained verifier (for example `aws-jwt-verify`), checking token use and client ID.
- DynamoDB: design keys from the access patterns; use high-cardinality partition keys; read with `Query` on a key or index, never `Scan` in a request path; enforce uniqueness and optimistic locking with `ConditionExpression`.
- DynamoDB tables: on-demand capacity unless steady load has been measured; enable point-in-time recovery and deletion protection on tables that hold data.
- S3: keep Block Public Access on and ACLs disabled (the defaults); serve public files through CloudFront with Origin Access Control (OAC), not a public bucket or the legacy OAI.
- Give clients presigned URLs for uploads and downloads instead of credentials; enable versioning and lifecycle rules on buckets that hold data.
- CloudFront: use cache and origin request policies, not legacy `ForwardedValues`; prefer content-hashed file names over routine invalidations (1,000 free paths per month).
- Cost: an AWS Budgets alert and a Cost Anomaly Detection monitor per account; send S3 and DynamoDB traffic from a VPC through gateway endpoints instead of a NAT gateway, which charges per GB.

## Verify
- `aws sts get-caller-identity --profile <name>` shows the expected account and role before any command against an account.
- The owning IaC tool's checks (Terraform: per the `terraform` pack).
- `aws accessanalyzer validate-policy --policy-type IDENTITY_POLICY --policy-document file://<policy>.json` for new or changed IAM policies; `aws iam simulate-custom-policy` to test one action.
- Add `--dryrun` to `aws s3 rm` and `aws s3 sync` first and report what would change.
- Never run commands that create, change, or delete cloud resources (deploys, `delete-*`, `update-*`, `aws s3 rm|rb|sync --delete`) without explicit user confirmation.

## Pitfalls
- Widening a policy to `"Action": "*"` or `"Resource": "*"` to clear `AccessDenied`; grant the exact action named in the error.
- An OIDC trust policy without a `sub` condition lets any repository on the provider assume the role; pin `aud` to `sts.amazonaws.com` and `sub` to the repository and branch or environment.
- A function writing to the bucket, queue, or table that triggers it loops. Recursive loop detection stops SQS, SNS, S3, and EventBridge custom event bus loops after about 16 cycles, but not DynamoDB stream loops; guard the trigger.
- API Gateway ends requests at 30 s (HTTP API, fixed) or 29 s (REST default); a longer function timeout keeps running and billing after the client got a 504. Move long work behind a queue.
- Sending large data through Lambda invocation payloads, which are capped (see «Version Notes»); pass it through S3.
- A global secondary index is eventually consistent and rejects `ConsistentRead`; read the base table when read-after-write matters.
- TTL deletes expired items within a few days and reads still return them until then; filter on the TTL attribute.
- `aws s3 rb --force` fails on a versioned bucket because it leaves object versions; `aws s3 sync --delete` removes target objects missing from the source.
- Terraform `aws_dynamodb_table` defaults to provisioned capacity with deletion protection and point-in-time recovery off; set all three explicitly.

## Version Notes
- Lambda runtimes: Node.js 24 and 22, Python 3.11 to 3.14, and .NET 10 are supported; Node.js 20 and Python 3.9 are deprecated, Python 3.10 deprecates on 2026-10-31 and .NET 8 on 2026-11-10. Check the deprecation date before choosing a runtime (as of 2026-10, per the Lambda runtimes page).
- Lambda invocation payloads are capped at 6 MB per synchronous request and response and 1 MB per asynchronous event (as of 2026-10, per Lambda quotas).
- The Lambda docs call standard functions "Event Functions" since 2026-10 and add "Web Functions" for HTTP; APIs, ARNs, and IaC resources still say "function" (as of 2026-10, per the Web Functions announcement).
- Terraform AWS provider v6 (6.0.0, 2025-06-18) adds a per-resource `region` argument that replaces per-Region provider aliases; read the version 6 upgrade guide before moving from v5 (as of 2026-10, per the provider changelog).
- Terraform's S3 backend locks with `use_lockfile = true` (Terraform 1.11+); the DynamoDB lock table (`dynamodb_table`) is deprecated (as of 2026-10, per the Terraform S3 backend docs).
- `configure-aws-credentials` v6 runs on Node 24 and needs runner 2.327.1+. GitHub's immutable OIDC `sub` format (`repo:<owner>@<id>/<repo>@<id>:...`) is the default for repositories created after 2026-07-15, and trust policies written for the old form reject it (as of 2026-10, per the action's releases and the GitHub changelog).
- `aws login` (CLI 2.32+) signs in with console credentials as an alternative to SSO profiles (as of 2026-10, per its AWS What's New announcement).
- S3 blocks SSE-C for new buckets since 2026-04, on top of Block Public Access and disabled ACLs since 2023-04 (as of 2026-10, per the S3 SSE-C FAQ and the 2023 announcement).
- Cognito feature plans Lite, Essentials, and Plus exist since 2024-11; new user pools default to Essentials, and managed login needs Essentials or Plus (as of 2026-10, per the Cognito feature plans page).
