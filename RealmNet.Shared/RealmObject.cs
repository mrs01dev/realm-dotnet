/* Copyright 2015 Realm Inc - All Rights Reserved
 * Proprietary and Confidential
 */
 
using System;
using System.Runtime.InteropServices;

namespace RealmNet
{
    public class RealmObject
    {
        private Realm _realm;
        private RowHandle _rowHandle;

        internal RowHandle RowHandle => _rowHandle;

        internal void _Manage(Realm realm, RowHandle rowHandle)
        {
            _realm = realm;
            _rowHandle = rowHandle;
        }

        protected T GetValue<T>(string propertyName)
        {
            if (_realm == null)
                throw new Exception("This object is not managed. Create through CreateObject");

            var tableHandle = _realm._tableHandles[GetType()];
            var columnIndex = NativeTable.get_column_index(tableHandle, propertyName, (IntPtr)propertyName.Length);
            var rowIndex = _rowHandle.RowIndex;

            if (typeof(T) == typeof(string))
            {
                long bufferSizeNeededChars = 16;
                IntPtr buffer;
                long currentBufferSizeChars;

                do
                {
                    buffer = MarshalHelpers.StrAllocateBuffer(out currentBufferSizeChars, bufferSizeNeededChars);
                    bufferSizeNeededChars = (long)NativeTable.get_string(tableHandle, columnIndex, (IntPtr)rowIndex, buffer,
                            (IntPtr)currentBufferSizeChars);

                } while (MarshalHelpers.StrBufferOverflow(buffer, currentBufferSizeChars, bufferSizeNeededChars));
                return (T)Convert.ChangeType(MarshalHelpers.StrBufToStr(buffer, (int)bufferSizeNeededChars), typeof(T));
            }
            if (typeof(T) == typeof(bool))
            {
                var value = MarshalHelpers.IntPtrToBool( NativeTable.get_bool(tableHandle, columnIndex, (IntPtr)rowIndex) );
                return (T)Convert.ChangeType(value, typeof(T));
            }
            if (typeof(T) == typeof(int))  // System.Int32 regardless of process being 32 or 64bit
            {
                var value = NativeTable.get_int64(tableHandle, columnIndex, (IntPtr)rowIndex);
                return (T)Convert.ChangeType(value, typeof(T));
            }
            if (typeof(T) == typeof(Int64)) 
            {
                var value = NativeTable.get_int64(tableHandle, columnIndex, (IntPtr)rowIndex);
                return (T)Convert.ChangeType(value, typeof(T));
            }
            if (typeof(T) == typeof(float)) 
            {
                var value = NativeTable.get_float(tableHandle, columnIndex, (IntPtr)rowIndex);
                return (T)Convert.ChangeType(value, typeof(T));
            }
            if (typeof(T) == typeof(double)) 
            {
                var value = NativeTable.get_double(tableHandle, columnIndex, (IntPtr)rowIndex);
                return (T)Convert.ChangeType(value, typeof(T));
            }
            if (typeof(T) == typeof(DateTimeOffset))
            {
                var unixTimeSeconds = NativeTable.get_datetime_seconds(tableHandle, columnIndex, (IntPtr)rowIndex);
                var value = DateTimeOffsetExtensions.FromUnixTimeSeconds(unixTimeSeconds);
                return (T)(object)value;
            }
            else
                throw new Exception ("Unsupported type " + typeof(T).Name);
        }

        protected void SetValue<T>(string propertyName, T value)
        {
            if (_realm == null)
                throw new Exception("This object is not managed. Create through CreateObject");

            if (!_realm.IsInTransaction)
                throw new RealmOutsideTransactionException("Cannot set values outside transaction");

            var tableHandle = _realm._tableHandles[GetType()];
            var columnIndex = NativeTable.get_column_index(tableHandle, propertyName, (IntPtr)propertyName.Length);
            var rowIndex = _rowHandle.RowIndex;

            if (typeof(T) == typeof(string)) 
            {
                var str = value as string;
                NativeTable.set_string (tableHandle, columnIndex, (IntPtr)rowIndex, str, (IntPtr)(str?.Length ?? 0));
            } 
            else if (typeof(T) == typeof(bool)) 
            {
                var marshalledValue = MarshalHelpers.BoolToIntPtr ((bool)Convert.ChangeType (value, typeof(bool)));
                NativeTable.set_bool (tableHandle, columnIndex, (IntPtr)rowIndex, marshalledValue);
            } 
            else if (typeof(T) == typeof(int)) 
            {  // System.Int32 regardless of process being 32 or 64bit
                Int64 marshalledValue = Convert.ToInt64 (value);
                NativeTable.set_int64 (tableHandle, columnIndex, (IntPtr)rowIndex, marshalledValue);
            } 
            else if (typeof(T) == typeof(Int64)) 
            {
                Int64 marshalledValue = Convert.ToInt64 (value);
                NativeTable.set_int64 (tableHandle, columnIndex, (IntPtr)rowIndex, marshalledValue);
            } 
            else if (typeof(T) == typeof(float)) 
            {
                float marshalledValue = Convert.ToSingle (value);
                NativeTable.set_float (tableHandle, columnIndex, (IntPtr)rowIndex, marshalledValue);
            } 
            else if (typeof(T) == typeof(double)) 
            {
                double marshalledValue = Convert.ToDouble (value);
                NativeTable.set_double (tableHandle, columnIndex, (IntPtr)rowIndex, marshalledValue);
            }
            else if (typeof(T) == typeof(DateTimeOffset))
            {
                Int64 marshalledValue = ((DateTimeOffset)(object)value).ToUnixTimeSeconds();
                NativeTable.set_datetime_seconds(tableHandle, columnIndex, (IntPtr)rowIndex, marshalledValue);
            }
            else
                throw new Exception ("Unsupported type " + typeof(T).Name);
        }


        protected T GetListValue<T>(string propertyName) where T : RealmList<RealmObject>
        {
            if (_realm == null)
                throw new Exception("This object is not managed. Create through CreateObject");

            var tableHandle = _realm._tableHandles[GetType()];
            var columnIndex = NativeTable.get_column_index(tableHandle, propertyName, (IntPtr)propertyName.Length);
            var listHandle = tableHandle.TableLinkList (columnIndex, _rowHandle);
            var ret = (T)Activator.CreateInstance(typeof(T));
            ret.CompleteInit (this, listHandle);
            return ret;
        }

        protected void SetListValue<T>(string propertyName, T value) where T : RealmList<RealmObject>
        {
            throw new NotImplementedException ("Setting a relationship list is not yet implemented");
        }


        /**
         * Shared factory to make an object in the realm from a known row
         * @param rowPtr may be null if a relationship lookup has failed.
        */ 
        internal RealmObject MakeRealmObject(System.Type objectType, IntPtr rowPtr) {
            if (rowPtr == (IntPtr)0)
                return null;  // typically no related object
            RealmObject ret = (RealmObject)Activator.CreateInstance(objectType);
            var relatedHandle = Realm.CreateRowHandle (rowPtr);
            ret._Manage(_realm, relatedHandle);
            return ret;
        }

        protected T GetObjectValue<T>(string propertyName) where T : RealmObject
        {
            if (_realm == null)
                throw new Exception("This object is not managed. Create through CreateObject");

            var tableHandle = _realm._tableHandles[GetType()];
            var columnIndex = NativeTable.get_column_index(tableHandle, propertyName, (IntPtr)propertyName.Length);
            var rowIndex = _rowHandle.RowIndex;
            var linkedRowPtr = NativeTable.get_link (tableHandle, columnIndex, (IntPtr)rowIndex);
            return (T)MakeRealmObject(typeof(T), linkedRowPtr);
        }

        // TODO make not generic
        protected void SetObjectValue<T>(string propertyName, T value) where T : RealmObject
        {
            if (_realm == null)
                throw new Exception("This object is not managed. Create through CreateObject");

            if (!_realm.IsInTransaction)
                throw new RealmOutsideTransactionException("Cannot set values outside transaction");

            var tableHandle = _realm._tableHandles[GetType()];
            var columnIndex = NativeTable.get_column_index(tableHandle, propertyName, (IntPtr)propertyName.Length);
            var rowIndex = _rowHandle.RowIndex;
            if (value==null)
                NativeTable.clear_link (tableHandle, columnIndex, (IntPtr)rowIndex);
            else
                NativeTable.set_link (tableHandle, columnIndex, (IntPtr)rowIndex, (IntPtr)value.RowHandle.RowIndex);

        }


        public override bool Equals(object p)
        {
            // If parameter is null, return false. 
            if (ReferenceEquals(p, null))
            {
                return false;
            }

            // Optimization for a common success case. 
            if (ReferenceEquals(this, p))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false. 
            if (this.GetType() != p.GetType())
                return false;

            // Return true if the fields match. 
            // Note that the base class is not invoked because it is 
            // System.Object, which defines Equals as reference equality. 
            return RowHandle.Equals(((RealmObject)p).RowHandle);
        }
    }
}
