namespace HarryTools.ServiceLocator.Example.Dev {
    public class DevScopeExample : ServiceScope {
        protected override void Configure(IServiceLocator _locator) {
            // Aquí se registran los servicios
            _locator.Register<IExampleService>(new ExampleManager());
        }
    }
}