using System.Runtime.CompilerServices;

// Lets the test assemblies reach the internal seams.
[assembly: InternalsVisibleTo("PetShop.Tests.EditMode")]
[assembly: InternalsVisibleTo("PetShop.Tests.PlayMode")]
