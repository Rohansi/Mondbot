using System;
using System.Collections.Generic;
using System.Dynamic;

namespace MondBot
{
    public sealed class SqlResult : DynamicObject
    {
        private readonly Dictionary<string, object> _columns;

        public SqlResult(IList<string> names, IList<object> values)
        {
            _columns = new Dictionary<string, object>();

            for (var i = 0; i < names.Count; i++)
            {
                _columns.Add(names[i], values[i]);
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var found = _columns.TryGetValue(binder.Name, out result);

            if (found && result == DBNull.Value)
                result = null;

            return found;
        }
    }
}
