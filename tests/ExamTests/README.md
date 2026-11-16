# ExamTests

Tests written for the exam, covering the RabbitMQ messaging between the monolith and the CatalogMicroservice, plus one HTTP endpoint test.

Run them with:

```
dotnet test tests/ExamTests
```

## The tests

| File | Kind | What it checks (one line) |
|---|---|---|
| `MessagingTest.cs` | Unit | The messaging wrapper sends the reply to the reply queue, with the same correlation id as the request. |
| `DeleteCatalogItemHandlerTests.cs` | Unit | When the catalog item exists, the microservice's delete handler deletes it through the repository and returns `true`. |
| `ListCatalogItemsReplyTests.cs` | Unit | When a "list catalog items" message arrives, the microservice sends a reply, with the same correlation id, containing the items. |
| `CatalogBrandListEndpointTest.cs` | Integration | `GET api/catalog-brands` returns `200 OK` and the 5 seeded brands. |

## How a message flows (and which test covers which step)

Example: deleting a catalog item when the feature toggle `UseCatalogMicroservice` is on.

1. The monolith calls `RequestAsync("catalog.delete-item", ...)`, which sends a message to RabbitMQ.
2. The microservice picks the message up from the queue.
3. `ReplyingMessageHandler` unwraps the message and calls the handler's `HandleAsync(...)`. **(MessagingTest)**
4. The handler does the work, e.g. deletes the item. **(DeleteCatalogItemHandlerTests)**
5. `ReplyingMessageHandler` sends the result back on the reply queue. **(MessagingTest)**
6. The monolith receives the reply and matches it by correlation id.

`ListCatalogItemsReplyTests` covers steps 3 to 5 together, using a real handler. None of the tests use a real RabbitMQ: the sender is faked. A test against a real RabbitMQ would be a bigger integration test.

The handlers are registered for their queues in `CatalogMicroservice/Program.cs`, e.g.:

```csharp
builder.Services.AddRequestHandler<DeleteCatalogItemRequest, bool, DeleteCatalogItemHandler>("catalog.delete-item");
```

## Words and concepts

- **Arrange / Act / Assert**: the three steps of a test. Arrange sets things up, Act calls the code being tested, and Assert checks the result.
- **`sut`**: short for **System Under Test**, the one real object the test is checking. Everything else is a fake. It's only a naming convention; `handler` works just as well.
- **Substitute / mock / fake**: a stand-in object created with `Substitute.For<IInterface>()`. It does nothing on its own, but you can tell it what to return and check afterwards what it was called with.
- **`HandleAsync`**: the method the messaging plumbing calls when a message arrives. Calling it directly in a test is the same as "pretending a message arrived" without RabbitMQ.
- **Correlation id**: a `Guid` put on every request. The reply carries the same id so the sender knows which request it answers.
- **The `42` in `MessagingTest`**: just an arbitrary example payload. The fake handler is told "when called with 42, return `"svar"`", and the request is sent with `Payload = 42`. The two must match. If the wrapper passed anything else to the handler, the fake would return `null` and the test would fail, so it also proves the payload is passed through unchanged.

## NSubstitute vs Moq

This repo uses **NSubstitute**. Course examples are often in **Moq**. They translate like this:

| Moq | NSubstitute |
|---|---|
| `var m = new Mock<IFoo>();` then `m.Object` | `var m = Substitute.For<IFoo>();` (use `m` directly) |
| `m.Setup(x => x.Get(1)).Returns(5);` | `m.Get(1).Returns(5);` |
| `It.IsAny<T>()` | `Arg.Any<T>()` |
| `It.Is<T>(x => ...)` | `Arg.Is<T>(x => ...)` |
| `m.Setup(x => x.Publish(...)).Verifiable();` + later `m.Verify();` | Only at the end: `m.Received(1).Publish(...);` |
| `m.Verify(x => x.Foo(), Times.Never)` | `m.DidNotReceive().Foo();` |

Notes:
- In NSubstitute there is no Setup step for calls that return nothing or `Task`. All calls are recorded automatically; you only check them in Assert.
- `Received(1)` means *exactly once*. Moq's `Verify()` means *at least once*. Use `Received()` with no number for "at least once".
- To *grab* an argument (e.g. the reply that was sent), use `Arg.Do<T>(x => captured = x)`, as `ListCatalogItemsReplyTests` does.

## Endpoint test: the A / B / C steps

`CatalogBrandListEndpointTest` follows the task steps:

- **A)** `new WebApplicationFactory<Program>()` starts the PublicApi in memory. No real web server or port is involved.
- **B)** `factory.CreateClient()` gives an `HttpClient` that talks to that in-memory app.
- **C)** `client.GetAsync("api/catalog-brands")`, then assert the status code and deserialize the JSON to check the data.

Details:
- `appsettings.test.json` (`UseOnlyInMemoryDatabase: true`) makes the API use the in-memory database with seed data. The 5 brands (Azure, .NET, Visual Studio, SQL Server, Other) come from `src/Infrastructure/Data/CatalogContextSeed.cs`.
- `UseCatalogMicroservice` is `false` in PublicApi's `appsettings.json`, so the endpoint reads straight from the database and not through the microservice.
- There is no basket endpoint in the API (the basket is only an HTML page in Web), so the catalog brands endpoint is used instead.
- **Flaky-test fix (already applied):** PublicApi's `AddEndpoints()` (from the `MinimalApi.Endpoint` package) registers every `IEndpoint` class from *every assembly loaded in the process* (`AppDomain.CurrentDomain.GetAssemblies()`). All ExamTests run in one process, so if a CatalogMicroservice test ran first, `CatalogMicroservice.dll` was already loaded and PublicApi also tried to build the microservice's endpoints. That failed with `Unable to resolve service for type 'IUriComposer' ... UpdateCatalogItemEndpoint`. Whether it failed depended on test order, which made it flaky. The fix: in step A, `WithWebHostBuilder(... ConfigureTestServices(...))` removes every `IEndpoint` registration whose class isn't from PublicApi's own assembly. This only affects tests; in production PublicApi never loads the microservice.
- **Possible problem:** PublicApi registers the RabbitMQ messaging at startup. If that tries to connect and RabbitMQ isn't running, the app may fail to start in the test.

## Project setup: why `extern alias`

ExamTests references both `PublicApi` (which brings in the monolith's `ApplicationCore`) and `CatalogMicroservice`. Both define types with **exactly the same full names**, e.g. `Microsoft.eShopWeb.ApplicationCore.Interfaces.IAppLogger<T>`, so a normal reference to both gives:

```
The type 'IAppLogger<T>' exists in both 'ApplicationCore' and 'CatalogMicroservice'
```

The fix is in `ExamTests.csproj`:

```xml
<ProjectReference Include="..\..\CatalogMicroservice\CatalogMicroservice.csproj" Aliases="CatalogMs" />
```

That hides CatalogMicroservice's types unless a file explicitly asks for them:

```csharp
extern alias CatalogMs;   // must be the very first line of the file

using CatalogMs::CatalogMicroservice.applicationCore.Services;
```

Files without that line only see the monolith's types.
