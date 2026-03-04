module Ocis.Tests.TestCategorizationTests

open System
open NUnit.Framework
open Ocis.Tests.OcisDB
open Ocis.Tests.SequentialOperationTests
open Ocis.Tests.ErrorHandlingTests

[<TestFixture>]
type TestCategorizationTests () =

  let getCategories
    (testType : Type)
    (methodName : string)
    (parameterTypes : Type array)
    =
    let methodInfo = testType.GetMethod (methodName, parameterTypes)

    Assert.That (
      methodInfo,
      Is.Not.Null,
      $"Method not found: {testType.FullName}.{methodName}"
    )

    methodInfo.GetCustomAttributes (typeof<CategoryAttribute>, true)
    |> Seq.cast<CategoryAttribute>
    |> Seq.map _.Name
    |> Set.ofSeq

  let assertSlow
    (testType : Type)
    (methodName : string)
    (parameterTypes : Type array)
    =
    let categories =
      getCategories testType methodName parameterTypes

    Assert.That (
      categories.Contains "Slow",
      Is.True,
      $"Expected Slow category on {testType.Name}.{methodName}"
    )

  [<Test>]
  member _.MemoryFootprintTest_ShouldBeMarkedSlow () =
    assertSlow (typeof<OcisDBTests>) "MemoryFootprint_ShouldBeReasonable" [|
      typeof<int>
    |]

  [<Test>]
  member _.SequentialLongRunningTest_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<SequentialOperationTests>)
      "LongRunningOperations_ShouldCompleteSuccessfully"
      [||]

  [<Test>]
  member _.SequentialBackgroundOperationsTest_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<SequentialOperationTests>)
      "BackgroundOperations_ShouldHandleSequentialRequests"
      [||]

  [<Test>]
  member _.ErrorHandlingLargeSSTableFlush_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<ErrorHandlingTests>)
      "SSTableFlush_ShouldHandleLargeNumberOfEntries"
      [||]

  [<Test>]
  member _.ErrorHandlingFileSystemStress_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<ErrorHandlingTests>)
      "FileSystemStressTest_ShouldHandleManyFiles"
      [||]

  [<Test>]
  member _.ErrorHandlingMemoryPressure_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<ErrorHandlingTests>)
      "MemoryPressureTest_ShouldHandleLargeDatasets"
      [||]

  [<Test>]
  member _.ErrorHandlingLayeredWriteSimulation_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<ErrorHandlingTests>)
      "LayeredWrite_ShouldHandleAsyncOperationsAndSleeps"
      [||]

  [<Test>]
  member _.ErrorHandlingPostCompactionRead_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<ErrorHandlingTests>)
      "PostCompactionRead_ShouldHandleCompactionTriggering"
      [||]

  [<Test>]
  member _.ErrorHandlingRealisticWorkload_ShouldBeMarkedSlow () =
    assertSlow
      (typeof<ErrorHandlingTests>)
      "RealisticWorkload_ShouldHandleMixedOperations"
      [||]
