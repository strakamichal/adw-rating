using System.CommandLine;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class AliasCommands
{
    public static Command Create(Option<string> connectionOption)
    {
        var command = new Command("add", "Add entities");
        var aliasCommand = new Command("alias", "Add alias for a handler or dog");
        aliasCommand.Add(CreateHandlerAliasCommand(connectionOption));
        aliasCommand.Add(CreateDogAliasCommand(connectionOption));
        command.Add(aliasCommand);
        return command;
    }

    private static Command CreateHandlerAliasCommand(Option<string> connectionOption)
    {
        var handlerIdArg = new Argument<int>("handler-id") { Description = "Handler ID" };
        var aliasNameArg = new Argument<string>("alias-name") { Description = "Alias name" };

        var command = new Command("handler", "Add alias for a handler");
        command.Add(handlerIdArg);
        command.Add(aliasNameArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var handlerId = parseResult.GetValue(handlerIdArg);
            var aliasName = parseResult.GetValue(aliasNameArg)!;

            var services = new ServiceCollection();
            services.AddDataMssql(connectionString);
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            await using var provider = services.BuildServiceProvider();

            var handlerRepo = provider.GetRequiredService<IHandlerRepository>();
            var handler = await handlerRepo.GetByIdAsync(handlerId);
            if (handler is null)
            {
                Console.Error.WriteLine($"Handler {handlerId} not found.");
                return 1;
            }

            var aliasRepo = provider.GetRequiredService<IHandlerAliasRepository>();
            await aliasRepo.CreateAsync(new HandlerAlias
            {
                AliasName = aliasName,
                CanonicalHandlerId = handlerId,
                Source = AliasSource.Manual,
                CreatedAt = DateTime.UtcNow
            });

            Console.WriteLine($"Alias '{aliasName}' created for handler [{handler.Id}] {handler.Name}.");
            return 0;
        });

        return command;
    }

    private static Command CreateDogAliasCommand(Option<string> connectionOption)
    {
        var dogIdArg = new Argument<int>("dog-id") { Description = "Dog ID" };
        var aliasNameArg = new Argument<string>("alias-name") { Description = "Alias name" };
        var typeOption = new Option<DogAliasType>("--type", "Alias type") { Required = true };

        var command = new Command("dog", "Add alias for a dog");
        command.Add(dogIdArg);
        command.Add(aliasNameArg);
        command.Add(typeOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var dogId = parseResult.GetValue(dogIdArg);
            var aliasName = parseResult.GetValue(aliasNameArg)!;
            var aliasType = parseResult.GetValue(typeOption);

            var services = new ServiceCollection();
            services.AddDataMssql(connectionString);
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            await using var provider = services.BuildServiceProvider();

            var dogRepo = provider.GetRequiredService<IDogRepository>();
            var dog = await dogRepo.GetByIdAsync(dogId);
            if (dog is null)
            {
                Console.Error.WriteLine($"Dog {dogId} not found.");
                return 1;
            }

            var aliasRepo = provider.GetRequiredService<IDogAliasRepository>();
            await aliasRepo.CreateAsync(new DogAlias
            {
                AliasName = aliasName,
                CanonicalDogId = dogId,
                AliasType = aliasType,
                Source = AliasSource.Manual,
                CreatedAt = DateTime.UtcNow
            });

            Console.WriteLine($"Alias '{aliasName}' ({aliasType}) created for dog [{dog.Id}] {dog.CallName}.");
            return 0;
        });

        return command;
    }
}
