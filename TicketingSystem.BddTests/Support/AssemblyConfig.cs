using Xunit;

// Disable test parallelization because tests share a common Redis instance
// and modify identical keys (e.g., ConcertId "C101" and AreaId "A1")
[assembly: CollectionBehavior(DisableTestParallelization = true)]
