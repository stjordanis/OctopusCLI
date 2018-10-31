﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Tests.Helpers;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class ApiCommandFixture : ApiCommandFixtureBase
    {
        DummyApiCommand apiCommand;

        [SetUp]
        public void SetUp()
        {
            apiCommand = new DummyApiCommand(RepositoryFactory, FileSystem, ClientFactory, CommandOutputProvider);
        }

        [Test]
        public void ShouldThrowIfNoServerSpecified()
        {
            Environment.SetEnvironmentVariable(ApiCommand.ServerUrlEnvVar, "");
            Assert.Throws<CommandException>(() => apiCommand.Execute("--apiKey=ABCDEF123456789"));    
        }

        [Test]
        public void ShouldThrowIfNoApiKeySpecified()
        {
            Environment.SetEnvironmentVariable(ApiCommand.ApiKeyEnvVar, "");
            Assert.Throws<CommandException>(() => apiCommand.Execute("--server=http://the-server"));
        }
        
        [Test]
        public void ShouldNotThrowIfApiKeySetInEnvVar()
        {
            Environment.SetEnvironmentVariable(ApiCommand.ApiKeyEnvVar, "whatever");
            apiCommand.Execute("--server=http://the-server");
        }
        
        [Test]
        public void ShouldNotThrowIfServerSetInEnvVar()
        {
            Environment.SetEnvironmentVariable(ApiCommand.ServerUrlEnvVar, "http://whatever");
            apiCommand.Execute("--apiKey=ABCDEF123456789");
        }

        [Test]
        public void ShouldThrowIfInvalidCommandLineParametersArePassed()
        {
            CommandLineArgs.Add("--fail=epic");
            Func<Task> exec = () => apiCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>();
        }

        [Test]
        public Task ShouldNotThrowIfCustomOptionsAreAddedByCommand()
        {
            CommandLineArgs.Add("--pill=red");
            return apiCommand.Execute(CommandLineArgs.ToArray());
        }

        [Test]
        public Task ShouldExecuteCommandWhenCorrectCommandLineParametersArePassed()
        {
            return apiCommand.Execute(CommandLineArgs.ToArray());
        }

        [Test]
        public Task ShouldExecuteCommandWhenCorrectCommandLineParametersArePassedWithSpaceName()
        {
            var client = Substitute.For<IOctopusAsyncClient>();
            ClientFactory.CreateAsyncClient(null).ReturnsForAnyArgs(client);

            Repository.Spaces.FindByName(Arg.Any<string>()).Returns(new SpaceResource {Id = "Spaces-2"});
            client.ForSystem().Returns(Repository);

            apiCommand = new DummyApiCommand(RepositoryFactory, FileSystem, ClientFactory, CommandOutputProvider);
            var argsWithSpaceName = CommandLineArgs.Concat(new []{"--space=abc"});
            return apiCommand.Execute(argsWithSpaceName.ToArray());
        }

        [Test]
        public async Task ShouldRunWithinASpaceWhenSpaceNameSpecified()
        {
            var client = Substitute.For<IOctopusAsyncClient>();
            ClientFactory.CreateAsyncClient(null).ReturnsForAnyArgs(client);

            Repository.Spaces.FindByName(Arg.Any<string>()).Returns(new SpaceResource { Id = "Spaces-2", Name = "test" });
            client.ForSystem().Returns(Repository);

            apiCommand = new DummyApiCommand(RepositoryFactory, FileSystem, ClientFactory, CommandOutputProvider);
            var argsWithSpaceName = CommandLineArgs.Concat(new[] { "--space=test" });
            bool isInRightSpaceContext = false;
            RepositoryFactory.CreateRepository(client, Arg.Do<RepositoryScope>(x =>
            {
                x.Apply(spaceId =>
                {
                    spaceId.Should().Be("Spaces-2");
                    isInRightSpaceContext = true;
                }, () => { }, () => { });
            }));

            await apiCommand.Execute(argsWithSpaceName.ToArray()).ConfigureAwait(false);
            Assert.IsTrue(isInRightSpaceContext);
            
        }

        [Test]
        public void ShouldThrowWhenThereIsNoSpaceMatchTheProvidedName()
        {
            var client = Substitute.For<IOctopusAsyncClient>();
            ClientFactory.CreateAsyncClient(null).ReturnsForAnyArgs(client);

            Repository.Spaces.FindByName("test").Returns(new SpaceResource { Id = "Spaces-2", Name = "test" });
            client.ForSystem().Returns(Repository);

            apiCommand = new DummyApiCommand(RepositoryFactory, FileSystem, ClientFactory, CommandOutputProvider);
            var argsWithSpaceName = CommandLineArgs.Concat(new[] { "--space=notExisted" });

            Func<Task> action = async () => await apiCommand.Execute(argsWithSpaceName.ToArray()).ConfigureAwait(false);
            action.ShouldThrow<CommandException>().WithMessage("Cannot find the space with name notExisted");
        }
    }
}
