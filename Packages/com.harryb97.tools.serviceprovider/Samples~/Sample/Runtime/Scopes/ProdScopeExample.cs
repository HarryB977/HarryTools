namespace HarryTools.ServiceLocator.Example.Prod {
    public class ProdScopeExample : ServiceScope {
        protected override void Configure(IServiceLocator _locator) {
            // Aquí se registran los servicios
            _locator.Register<IExampleService>(new ExampleManager());
        }
    }
}