using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Discord_UWP
{
    public interface IMessageHandler
    {
        Task<MessageFormat> HandleMessage(JToken msg);
    }

    public class MessageHandler<TInput> : IMessageHandler
    {
        private object _handler;

        //public MessageHandler(HandlerFunction function)
        //{
        //    _handler = function;
        //}

        public MessageHandler(HandlerOnlyFunction function)
        {
            _handler = function;
        }

        public delegate Task<MessageFormat> HandlerFunction(TInput input);
        public delegate void HandlerOnlyFunction(TInput input);

        public Task<MessageFormat> HandleMessage(JToken msg)
        {
            var hof = _handler as HandlerOnlyFunction;
            if (hof != null)
            {
                hof(msg.ToObject<TInput>());
                return Task.FromResult<MessageFormat>(null);
            }

            var hf = _handler as HandlerFunction;
            if (hf != null)
            {
                return hf(msg.ToObject<TInput>());
            }

            throw new InvalidOperationException("Handler of non existing type");
        }
    }
}
