namespace ARH.Tests.Integration;

[CollectionDefinition(
    "MySQL integration",
    DisableParallelization = true)]
public sealed class MySqlCollection
    : ICollectionFixture<MySqlFixture>
{
}