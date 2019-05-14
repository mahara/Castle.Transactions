namespace Castle.Services.Transaction.Tests
{
	#region Using Directives

	using System.Threading;

	using NUnit.Framework;

	#endregion

	public class MiscTests
	{
		[Test]
		[Description("As we are working on the same folders, we don't want to run the tests concurrently.")]
		[Ignore("TODO: .NET Core Migration")]
		public void CheckSTA()
		{
			var aptState = Thread.CurrentThread.GetApartmentState();

			// This is somehow appear to be MTA.
			Assert.IsTrue(aptState == ApartmentState.STA);
		}
	}
}