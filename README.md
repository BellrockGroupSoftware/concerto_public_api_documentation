# Concerto Public API Documentation

Documentation and worked examples for integrating third-party systems with the Concerto public API.

## What's here

|Item|Description|
|-|-|
|`IAC Concerto API Guide v2.4.pdf`|The API guide — endpoints, authentication, events and payload reference. Start here.|
|`actions_api_example`|A runnable C# example showing an end-to-end Actions API integration.|

## The Actions API example

A console app that walks, step by step, through a complete round trip between
Concerto and a fictional CAFM system ("MyCafm"):

1. **Receive** an `orderAssigned` event from Concerto and raise a request locally.
2. **Map** a local `allocated` event back to a Concerto action, by key or name — never by ID.
3. **Discover** which actions are available on the order.
4. **Describe** the chosen action to find out what inputs it needs.
5. **Resolve** those inputs into a payload, one resolver per input type.
6. **Validate** the payload, then **invoke** the action and see the order change.

Concerto and MyCafm are both mocked, so nothing talks to a real system. The point is
the shape of the integration: because inputs come from `describe` rather than being
hardcoded, a client changing their action configuration doesn't require a code change.

### Running it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project Actions
```

The app pauses between stages and explains what it's about to do. When it finishes it
suggests further things to try — the mock holds more actions than the one that runs,
including nested, array-shaped and multi-property inputs.

## Licence

MIT — see [LICENSE](LICENSE).

