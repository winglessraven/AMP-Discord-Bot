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

        public enum MyPluginPermissions
        {
            MyCustomPermission
        }

        [JSONMethod]
        [RequiresPermissions(MyPluginPermissions.MyCustomPermission)]
        public string GoodbyePerson(string Name) => $"Goodbye, {Name}!";

        [JSONMethod]
        public string HelloPerson(string Name) => $"Hello, {Name}. This is AMP!";

        //Using JSONResponse is optional, but allows for automatic parameter mapping in callbacks.
        [JSONResponse]
        public class ComplexResponse
        {
            public string Food { get; set; }
            public string Drink { get; set; }
        }

        public enum MealTypes
        {
            Breakfast,
            Lunch,
            Dinner,
            Afternoon_Tea,
        }

        // From Javascript, you can either pass the meal as string matching the Enum name, or as an int matching it's value.
        [JSONMethod("Get a food and drink for a given meal.")]
        public ComplexResponse GetMeal(MealTypes meal)
        {
            switch (meal)
            {
                case MealTypes.Afternoon_Tea:
                    //We are British after all.
                    return new ComplexResponse() { Food = "Cake", Drink = "Tea, Earl Grey, Hot." };
                case MealTypes.Breakfast:
                    return new ComplexResponse() { Food = "Toast", Drink = "Coffee" };
                case MealTypes.Lunch:
                    return new ComplexResponse() { Food = "Burger", Drink = "Soda" };
                case MealTypes.Dinner:
                default:
                    return new ComplexResponse() { Food = "Chicken", Drink = "Water" };
            }
        }

        RunningTask MyTask;

        private async void LongRunningTask(int delay)
        {
            MyTask = _tasks.CreateTask("Some Task", "It's gonna take a while...");
            await Task.Delay(delay);
            MyTask.End();
            MyTask = null;
        }

        //Web accessible methods should return very quickly. Any long-running tasks
        //should be performed in the background.
        [JSONMethod("Perform a fake long-running task, but only if one isn't currently running.", "A boolean indicating whether or not the task was started.")]
        //The description and 'returns' are optional, but handy. They show up in GetAPISpec.
        public bool DoLongRunningTask(
            [ParameterDescription("How long to wait in milliseconds")] int delay) //Like the JSONMethod params, ParameterDescriptions are optional - but handy. They show up in GetAPISpec.
        {
            if (MyTask != null)
            {
                return false;
            }
            else
            {
                LongRunningTask(delay);
                return true;
            }
        }
    }
}
