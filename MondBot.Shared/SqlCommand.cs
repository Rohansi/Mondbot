using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;

namespace MondBot
{
    public sealed class SqlCommand : IDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly bool _ownsConnection;
        private readonly NpgsqlCommand _command;

        public SqlCommand(string sql)
            : this(Database.CreateConnection(), true, sql)
        {

        }

        public SqlCommand(NpgsqlTransaction transaction, string sql)
            : this(transaction.Connection, false, sql)
        {
            
        }

        private SqlCommand(NpgsqlConnection connection, bool ownsConnection, string sql)
        {
            _connection = connection;
            _ownsConnection = ownsConnection;
            _command = new NpgsqlCommand(sql, _connection);
        }

        public void Dispose()
        {
            _command.Dispose();

            if (_ownsConnection)
                _connection.Dispose();
        }

        public object this[string name]
        {
            set
            {
                var idx = _command.Parameters.IndexOf(name);

                if (idx != -1)
                    _command.Parameters[idx].Value = value;

                _command.Parameters.AddWithValue(name, value);
            }
        }

        public async Task<IEnumerable<dynamic>> Execute()
        {
            var reader = await _command.ExecuteReaderAsync();
            return new ResultIterator(_command.Connection, reader);
        }

        private struct ResultIterator : IEnumerable<object>, IEnumerator<object>
        {
            private readonly NpgsqlConnection _connection;
            private readonly DbDataReader _reader;
            private readonly string[] _names;
            private readonly object[] _values;

            public dynamic Current { get; private set; }

            public ResultIterator(NpgsqlConnection connection, DbDataReader reader)
            {
                _connection = connection;
                _reader = reader;

                _names = new string[_reader.FieldCount];
                _values = new object[_reader.FieldCount];

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    _names[i] = reader.GetName(i);
                }

                Current = null;
            }

            public void Dispose()
            {
                _connection.Dispose();
                _reader.Dispose();
            }

            public IEnumerator<object> GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public bool MoveNext()
            {
                if (!_reader.Read())
                    return false; // finished reading

                if (_reader.GetValues(_values) != _reader.FieldCount)
                    throw new Exception("failed to read column values");

                Current = new SqlResult(_names, _values);
                return true;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }
        }

        public async Task ExecuteNonQuery()
        {
            using (_command.Connection)
            {
                await _command.ExecuteNonQueryAsync();
            }
        }

        public async Task<object> ExecuteScalar()
        {
            using (_command.Connection)
            {
                return await _command.ExecuteScalarAsync();
            }
        }
    }
}
