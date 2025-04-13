namespace OllamaConfig
{
    public class Either<TSuccess, TFailure>
    {
        private readonly TSuccess _success;
        private readonly TFailure _failure;
        private readonly bool _isSuccess;

        public Either(TSuccess success)
        {
            _success = success;
            _isSuccess = true;
        }

        public Either(TFailure failure)
        {
            _failure = failure;
            _isSuccess = false;
        }

        public TResult Match<TResult>(Func<TSuccess, TResult> successFunc, Func<TFailure, TResult> failureFunc)
        {
            return _isSuccess ? successFunc(_success) : failureFunc(_failure);
        }

        public async Task<TResult> Match<TResult>(Func<TSuccess, Task<TResult>> successFunc, Func<TFailure, Task<TResult>> failureFunc)
        {
            return _isSuccess ? await successFunc(_success) : await failureFunc(_failure);
        }

        public bool IsSuccess => _isSuccess;

        public TSuccess Success => _success;
        public TFailure Error => _failure;
    }

}
