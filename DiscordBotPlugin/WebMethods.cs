using ModuleShared;
using System.Threading.Tasks;

namespace DiscordBotPlugin
{
    class WebMethods : WebMethodsBase
    {
        private readonly IRunningTasksManager _tasks;

        public WebMethods(IRunningTasksManager tasks)
        {
            _tasks = tasks;
        }
    }
}
