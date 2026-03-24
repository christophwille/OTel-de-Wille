# OpenTelemetry Tracing with EasyNetQ

Original idea: try to implement https://github.com/EasyNetQ/EasyNetQ/pull/1628#issuecomment-1590759688

Used instead: RabbitMQ Client OTel library.

## Running RabbitMQ

https://www.rabbitmq.com/docs/download

`podman run --rm -it --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management`

http://localhost:15672 with guest/guest

## Aspire Dashboard

`podman run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest`

## Sample Producer

https://localhost:7046/enqenque