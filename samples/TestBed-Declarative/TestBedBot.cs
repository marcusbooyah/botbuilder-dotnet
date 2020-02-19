// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.3.0

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Debugging;
using Microsoft.Bot.Builder.Dialogs.Declarative;
using Microsoft.Bot.Builder.Dialogs.Declarative.Loaders;
using Microsoft.Bot.Builder.Dialogs.Declarative.Resources;
using Microsoft.Bot.Schema;

namespace Microsoft.BotBuilderSamples
{
    public class TestBedBot : ActivityHandler
    {
        private IStatePropertyAccessor<DialogState> dialogStateAccessor;
        private readonly ResourceExplorer resourceExplorer;
        private DialogManager _dialogManager;

        public TestBedBot(ConversationState conversationState, ResourceExplorer resourceExplorer)
        {
            this.dialogStateAccessor = conversationState.CreateProperty<DialogState>("RootDialogState");
            this.resourceExplorer = resourceExplorer;

            // auto reload dialogs when file changes
            this.resourceExplorer.Changed += (resources) =>
            {
                if (resources.Any(resource => resource.Id == ".dialog"))
                {
                    Task.Run(() => this.LoadRootDialogAsync());
                }
            };

            LoadRootDialogAsync();
        }

        public async override Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _dialogManager.OnTurnAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        private void LoadRootDialogAsync()
        {
            System.Diagnostics.Trace.TraceInformation("Loading resources...");

            var resource = this.resourceExplorer.GetResource("rootQnA.dialog");
            _dialogManager = new DialogManager(this.resourceExplorer.LoadType<AdaptiveDialog>(resource));
            _dialogManager.UseResourceExplorer(this.resourceExplorer);
            _dialogManager.UseLanguageGeneration();

            System.Diagnostics.Trace.TraceInformation("Done loading resources.");
        }
    }
}
