// WinformValidation.cs: Contributed by Michael Weinhardt[mikedub@bigpond.com]
// A series of Validation controls a la Webform validation, translated
// to WinForms and hooking into existing WinForm validation infrastructure
// ie ErrorProvider component, Validating event etc

#region Copyright © 2002-2003 The Genghis Group
/*
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the authors be held liable for any damages arising from the
 * use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not claim
 * that you wrote the original software. If you use this software in a product,
 * an acknowledgment in the product documentation is IsRequired, as shown here:
 * 
 * Portions Copyright © 2002-2003 The Genghis Group (http://www.genghisgroup.com/).
 * 
 * 2. No substantial portion of the source code of this library may be redistributed
 * without the express written permission of the copyright holders, where
 * "substantial" is defined as enough code to be recognizably from this library. 
*/
#endregion
#region History

// 16 Jul 2002: Created project

#endregion
#region TODOs

// TODO: Get FxCop to perform a raid on this code
// TODO: Investigate nicer error display (ErrorProvider-style, Balloon(?), ValidationSummary)
// TODO: Can the validators somehow extend the container form with IsValid and Validate()?
// TODO: Property to force validation, irrespective of whether ControlToValidate had
//       been edited??? (Was in an early version, but removed due to inconsistency with
//       webform validation, but reconsider. ForceValidatingEvent property?  Property which 
//       allows for users setting CausesValidation to false on their ControlToValidate.
// TODO: should we select a hidden tabpage if one of its controls is invalid and its first in tab order??
// TODO: why doesn't focus work on a modeless form, it does actually work but is not shown immediately although
//       if you click to another form then back again, the focus is set as specified.

#endregion

#region Using directives

using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Web.UI.Design.WebControls;

#endregion

namespace Genghis.Windows.Forms
{
    #region ValidationDataType

    /// <summary>
    /// Specifies the validation data types used by the CompareValidator and RangeValidator controls.
    /// </summary>
    /// <remarks>
    /// The ValidationDataType enumeration represents the different data types that the CompareValidator 
    /// and RangeValidator controls can validate. When you specify a data type for validation control, if 
    /// the input control being validated passes validation, the value of the input control can parsed to 
    /// the specified data type.
    /// </remarks>
    public enum ValidationDataType
    {
        /// <summary>
        /// A monetary data type. The value is treated as a System.Decimal. However, 
        /// currency and grouping symbols are still allowed.
        /// </summary>
        Currency,
        /// <summary>
        /// A date data type. Only numeric dates are allowed. The time portion cannot be specified.
        ///</summary>
        Date,
        /// <summary>
        /// A double precision floating point number data type. The value is treated as a System.Double.
        ///</summary>
        Double,
        /// <summary>
        /// A 32-bit signed integer data type. The value is treated as a System.Int32.
        ///</summary>
        Integer,
        /// <summary>
        /// A string data type. The value is treated as a System.String.
        ///</summary>
        String
    }

    #endregion

    #region ValidationDepth

    /// <summary>
    /// Specifies what controls are included when the container is validated
    /// </summary>
    /// <remarks>
    /// The ValidationDepth enumeration specifies what <c>BaseValidators</c> are validated.
    /// </remarks>
    public enum ValidationDepth
    {
        /// <summary>
        /// Validate <c>BaseValidators</c> whose direct parent is <c>ContainerToValidate</c>.
        /// </summary>
        ContainerOnly,
        /// <summary>
        /// Validate <c>BaseValidators</c> whose direct or indirect parent is <c>ContainerToValidate</c>.
        ///</summary>
        All
    }

    #endregion

    #region ValidationCompareOperator

    /// <summary>
    /// Specifies the validation comparison operators used by the CompareValidator control.
    /// </summary>
    /// <remarks>
    /// The ValidationCompareOperator enumeration represents the comparison operations that 
    /// can be performed by the CompareValidator control.
    /// </remarks>
    public enum ValidationCompareOperator
    {
        /// <summary>A comparison for data type only.</summary>
        DataTypeCheck,
        /// <summary>A comparison for equality.</summary>
        Equal,
        /// <summary>A comparison for greater than.</summary>
        GreaterThan,
        /// <summary>A comparison for greater than or equal.</summary>
        GreaterThanEqual,
        /// <summary>A comparison for less than.</summary>
        LessThan,
        /// <summary>A comparison for less than or equal.</summary>
        LessThanEqual,
        /// <summary>A comparison for non-equality.</summary>
        NotEqual
    }

    #endregion

    #region ValidatableControlConverter

    /// <summary>
    /// Provides a type converter to convert object references to and from other representations.
    /// </summary>
    /// <remarks>
    /// This TypeConverter is designed and used by each of the validation controls to determine
    /// which controls can be selected for the <c>ControlToValidate</c> and <c>ControlToCompare</c>
    /// properties.  The only controls that can be used are:
    /// <list type="bullet">
    /// <item>TextBoxes</item>
    /// <item>ListBoxes</item>
    /// <item>ComboBoxes</item>
    /// </list>
    /// </remarks>
    partial class ValidatableControlConverter : ReferenceConverter
    {
        /// <summary>
        /// Initializes a new instance of the <c>ValidatableControlConverter</c> component.
        /// </summary>
        public ValidatableControlConverter(Type type) : base(type) { }

        /// <summary>
        /// Returns a value indicating whether a particular value can be added to the standard values collection
        /// of the base ReferenceConverter class.
        /// </summary>
        /// <remarks>
        /// The only values allowed include the following WinForm controls:
        /// <list type="bullet">
        /// <item>TextBoxes</item>
        /// <item>ListBoxes</item>
        /// <item>ComboBoxes</item>
        /// </list>
        /// </remarks>
        protected override bool IsValueAllowed(ITypeDescriptorContext context, object value)
        {
            return ((value is TextBox) ||
                (value is ListBox) ||
                (value is ComboBox) ||
                (value is UserControl) ||
                (value is MaskedTextBox));
        }
    }

    #endregion

    #region ContainerControlConverter

    /// <summary>
    /// Provides a type converter to convert object references to and from other representations.
    /// </summary>
    /// <remarks>
    /// This TypeConverter is designed and used by each of the validation controls to determine
    /// which controls can be selected for the <c>ControlToValidate</c> and <c>ControlToCompare</c>
    /// properties.  The only controls that can be used are:
    /// <list type="bullet">
    /// <item>Forms</item>
    /// <item>TabPages</item>
    /// </list>
    /// </remarks>
    partial class ContainerControlConverter : ReferenceConverter
    {
        /// <summary>
        /// Initializes a new instance of the <c>ContainerControlConverter</c> component.
        /// </summary>
        public ContainerControlConverter(Type type) : base(type) { }

        /// <summary>
        /// Returns a value indicating whether a particular value can be added to the standard values collection
        /// of the base ReferenceConverter class.
        /// </summary>
        /// <remarks>
        /// The only values allowed include the following Windows Forms controls:
        /// <list type="bullet">
        /// <item>Form</item>
        /// <item>GroupBox</item>
        /// <item>Panel</item>
        /// <item>TabControl</item>
        /// <item>TabPage</item>
        /// <item>UserControl</item>
        /// </list>
        /// </remarks>
        protected override bool IsValueAllowed(ITypeDescriptorContext context, object value)
        {
            return ((value is ScrollableControl) ||
                    (value is TabControl) ||
                    (value is System.Windows.Forms.GroupBox));
        }
    }

    #endregion

    #region BaseValidatorCollection (care of http://sellsbrothers.com/tools/#collectiongen)
    /// <summary>
    ///        A strongly-typed collection of <see cref="BaseValidator"/> objects.
    /// </summary>
    [Serializable]
    partial class BaseValidatorCollection : ICollection, IList, IEnumerable, ICloneable
    {
        #region Interfaces
        /// <summary>
        ///        Supports type-safe iteration over a <see cref="BaseValidatorCollection"/>.
        /// </summary>
        public interface IBaseValidatorCollectionEnumerator
        {
            /// <summary>
            ///        Gets the current element in the collection.
            /// </summary>
            BaseValidator Current { get;}

            /// <summary>
            ///        Advances the enumerator to the next element in the collection.
            /// </summary>
            /// <exception cref="InvalidOperationException">
            ///        The collection was modified after the enumerator was created.
            /// </exception>
            /// <returns>
            ///        <c>true</c> if the enumerator was successfully advanced to the next element; 
            ///        <c>false</c> if the enumerator has passed the end of the collection.
            /// </returns>
            bool MoveNext();

            /// <summary>
            ///        Sets the enumerator to its initial position, before the first element in the collection.
            /// </summary>
            void Reset();
        }
        #endregion

        private const int DEFAULT_CAPACITY = 16;

        #region Implementation (data)
        private BaseValidator[] m_array;
        private int m_count = 0;
        [NonSerialized]
        private int m_version = 0;
        #endregion

        #region Static Wrappers
        /// <summary>
        ///        Creates a synchronized (thread-safe) wrapper for a 
        ///     <c>BaseValidatorCollection</c> instance.
        /// </summary>
        /// <returns>
        ///     An <c>BaseValidatorCollection</c> wrapper that is synchronized (thread-safe).
        /// </returns>
        public static BaseValidatorCollection Synchronized(BaseValidatorCollection list)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            return new SyncBaseValidatorCollection(list);
        }

        /// <summary>
        ///        Creates a read-only wrapper for a 
        ///     <c>BaseValidatorCollection</c> instance.
        /// </summary>
        /// <returns>
        ///     An <c>BaseValidatorCollection</c> wrapper that is read-only.
        /// </returns>
        public static BaseValidatorCollection ReadOnly(BaseValidatorCollection list)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            return new ReadOnlyBaseValidatorCollection(list);
        }
        #endregion

        #region Construction
        /// <summary>
        ///        Initializes a new instance of the <c>BaseValidatorCollection</c> class
        ///        that is empty and has the default initial capacity.
        /// </summary>
        public BaseValidatorCollection()
        {
            m_array = new BaseValidator[DEFAULT_CAPACITY];
        }

        /// <summary>
        ///        Initializes a new instance of the <c>BaseValidatorCollection</c> class
        ///        that has the specified initial capacity.
        /// </summary>
        /// <param name="capacity">
        ///        The number of elements that the new <c>BaseValidatorCollection</c> is initially capable of storing.
        ///    </param>
        public BaseValidatorCollection(int capacity)
        {
            m_array = new BaseValidator[capacity];
        }

        /// <summary>
        ///        Initializes a new instance of the <c>BaseValidatorCollection</c> class
        ///        that contains elements copied from the specified <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="c">The <c>BaseValidatorCollection</c> whose elements are copied to the new collection.</param>
        public BaseValidatorCollection(BaseValidatorCollection c)
        {
            m_array = new BaseValidator[c.Count];
            AddRange(c);
        }

        /// <summary>
        ///        Initializes a new instance of the <c>BaseValidatorCollection</c> class
        ///        that contains elements copied from the specified <see cref="BaseValidator"/> array.
        /// </summary>
        /// <param name="a">The <see cref="BaseValidator"/> array whose elements are copied to the new list.</param>
        public BaseValidatorCollection(BaseValidator[] a)
        {
            m_array = new BaseValidator[a.Length];
            AddRange(a);
        }
        #endregion

        #region Operations (type-safe ICollection)
        /// <summary>
        ///        Gets the number of elements actually contained in the <c>BaseValidatorCollection</c>.
        /// </summary>
        public virtual int Count
        {
            get { return m_count; }
        }

        /// <summary>
        ///        Copies the entire <c>BaseValidatorCollection</c> to a one-dimensional
        ///        string array.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="BaseValidator"/> array to copy to.</param>
        public virtual void CopyTo(BaseValidator[] array)
        {
            this.CopyTo(array, 0);
        }

        /// <summary>
        ///        Copies the entire <c>BaseValidatorCollection</c> to a one-dimensional
        ///        <see cref="BaseValidator"/> array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="BaseValidator"/> array to copy to.</param>
        /// <param name="start">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        public virtual void CopyTo(BaseValidator[] array, int start)
        {
            if (m_count > array.GetUpperBound(0) + 1 - start)
                throw new System.ArgumentException("Destination array was not long enough.");

            Array.Copy(m_array, 0, array, start, m_count);
        }

        /// <summary>
        ///        Gets a value indicating whether access to the collection is synchronized (thread-safe).
        /// </summary>
        /// <returns>true if access to the ICollection is synchronized (thread-safe); otherwise, false.</returns>
        public virtual bool IsSynchronized
        {
            get { return m_array.IsSynchronized; }
        }

        /// <summary>
        ///        Gets an object that can be used to synchronize access to the collection.
        /// </summary>
        public virtual object SyncRoot
        {
            get { return m_array.SyncRoot; }
        }
        #endregion

        #region Operations (type-safe IList)
        /// <summary>
        ///        Gets or sets the <see cref="BaseValidator"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///        <para><paramref name="index"/> is less than zero</para>
        ///        <para>-or-</para>
        ///        <para><paramref name="index"/> is equal to or greater than <see cref="BaseValidatorCollection.Count"/>.</para>
        /// </exception>
        public virtual BaseValidator this[int index]
        {
            get
            {
                ValidateIndex(index); // throws
                return m_array[index];
            }
            set
            {
                ValidateIndex(index); // throws
                ++m_version;
                m_array[index] = value;
            }
        }

        /// <summary>
        ///        Adds a <see cref="BaseValidator"/> to the end of the <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="item">The <see cref="BaseValidator"/> to be added to the end of the <c>BaseValidatorCollection</c>.</param>
        /// <returns>The index at which the value has been added.</returns>
        public virtual int Add(BaseValidator item)
        {
            if (m_count == m_array.Length)
                EnsureCapacity(m_count + 1);

            m_array[m_count] = item;
            m_version++;

            return m_count++;
        }

        /// <summary>
        ///        Removes all elements from the <c>BaseValidatorCollection</c>.
        /// </summary>
        public virtual void Clear()
        {
            ++m_version;
            m_array = new BaseValidator[DEFAULT_CAPACITY];
            m_count = 0;
        }

        /// <summary>
        ///        Creates a shallow copy of the <see cref="BaseValidatorCollection"/>.
        /// </summary>
        public virtual object Clone()
        {
            BaseValidatorCollection newColl = new BaseValidatorCollection(m_count);
            Array.Copy(m_array, 0, newColl.m_array, 0, m_count);
            newColl.m_count = m_count;
            newColl.m_version = m_version;

            return newColl;
        }

        /// <summary>
        ///        Determines whether a given <see cref="BaseValidator"/> is in the <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="item">The <see cref="BaseValidator"/> to check for.</param>
        /// <returns><c>true</c> if <paramref name="item"/> is found in the <c>BaseValidatorCollection</c>; otherwise, <c>false</c>.</returns>
        public virtual bool Contains(BaseValidator item)
        {
            for (int i = 0; i != m_count; ++i)
                if (m_array[i].Equals(item))
                    return true;
            return false;
        }

        /// <summary>
        ///        Returns the zero-based index of the first occurrence of a <see cref="BaseValidator"/>
        ///        in the <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="item">The <see cref="BaseValidator"/> to locate in the <c>BaseValidatorCollection</c>.</param>
        /// <returns>
        ///        The zero-based index of the first occurrence of <paramref name="item"/> 
        ///        in the entire <c>BaseValidatorCollection</c>, if found; otherwise, -1.
        ///    </returns>
        public virtual int IndexOf(BaseValidator item)
        {
            for (int i = 0; i != m_count; ++i)
                if (m_array[i].Equals(item))
                    return i;
            return -1;
        }

        /// <summary>
        ///        Inserts an element into the <c>BaseValidatorCollection</c> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The <see cref="BaseValidator"/> to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///        <para><paramref name="index"/> is less than zero</para>
        ///        <para>-or-</para>
        ///        <para><paramref name="index"/> is equal to or greater than <see cref="BaseValidatorCollection.Count"/>.</para>
        /// </exception>
        public virtual void Insert(int index, BaseValidator item)
        {
            ValidateIndex(index, true); // throws

            if (m_count == m_array.Length)
                EnsureCapacity(m_count + 1);

            if (index < m_count)
            {
                Array.Copy(m_array, index, m_array, index + 1, m_count - index);
            }

            m_array[index] = item;
            m_count++;
            m_version++;
        }

        /// <summary>
        ///        Removes the first occurrence of a specific <see cref="BaseValidator"/> from the <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="item">The <see cref="BaseValidator"/> to remove from the <c>BaseValidatorCollection</c>.</param>
        /// <exception cref="ArgumentException">
        ///        The specified <see cref="BaseValidator"/> was not found in the <c>BaseValidatorCollection</c>.
        /// </exception>
        public virtual void Remove(BaseValidator item)
        {
            int i = IndexOf(item);
            if (i < 0)
                throw new System.ArgumentException("Cannot remove the specified item because it was not found in the specified Collection.");

            ++m_version;
            RemoveAt(i);
        }

        /// <summary>
        ///        Removes the element at the specified index of the <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///        <para><paramref name="index"/> is less than zero</para>
        ///        <para>-or-</para>
        ///        <para><paramref name="index"/> is equal to or greater than <see cref="BaseValidatorCollection.Count"/>.</para>
        /// </exception>
        public virtual void RemoveAt(int index)
        {
            ValidateIndex(index); // throws

            m_count--;

            if (index < m_count)
            {
                Array.Copy(m_array, index + 1, m_array, index, m_count - index);
            }

            // We can't set the deleted entry equal to null, because it might be a value type.
            // Instead, we'll create an empty single-element array of the right type and copy it 
            // over the entry we want to erase.
            BaseValidator[] temp = new BaseValidator[1];
            Array.Copy(temp, 0, m_array, m_count, 1);
            m_version++;
        }

        /// <summary>
        ///        Gets a value indicating whether the collection has a fixed size.
        /// </summary>
        /// <value>true if the collection has a fixed size; otherwise, false. The default is false</value>
        public virtual bool IsFixedSize
        {
            get { return false; }
        }

        /// <summary>
        ///        gets a value indicating whether the IList is read-only.
        /// </summary>
        /// <value>true if the collection is read-only; otherwise, false. The default is false</value>
        public virtual bool IsReadOnly
        {
            get { return false; }
        }
        #endregion

        #region Operations (type-safe IEnumerable)

        /// <summary>
        ///        Returns an enumerator that can iterate through the <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <returns>An <see cref="Enumerator"/> for the entire <c>BaseValidatorCollection</c>.</returns>
        public virtual IBaseValidatorCollectionEnumerator GetEnumerator()
        {
            return new Enumerator(this);
        }
        #endregion

        #region Public helpers (just to mimic some nice features of ArrayList)

        /// <summary>
        ///        Gets or sets the number of elements the <c>BaseValidatorCollection</c> can contain.
        /// </summary>
        public virtual int Capacity
        {
            get { return m_array.Length; }

            set
            {
                if (value < m_count)
                    value = m_count;

                if (value != m_array.Length)
                {
                    if (value > 0)
                    {
                        BaseValidator[] temp = new BaseValidator[value];
                        Array.Copy(m_array, temp, m_count);
                        m_array = temp;
                    }
                    else
                    {
                        m_array = new BaseValidator[DEFAULT_CAPACITY];
                    }
                }
            }
        }

        /// <summary>
        ///        Adds the elements of another <c>BaseValidatorCollection</c> to the current <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="x">The <c>BaseValidatorCollection</c> whose elements should be added to the end of the current <c>BaseValidatorCollection</c>.</param>
        /// <returns>The new <see cref="BaseValidatorCollection.Count"/> of the <c>BaseValidatorCollection</c>.</returns>
        public virtual int AddRange(BaseValidatorCollection x)
        {
            if (m_count + x.Count >= m_array.Length)
                EnsureCapacity(m_count + x.Count);

            Array.Copy(x.m_array, 0, m_array, m_count, x.Count);
            m_count += x.Count;
            m_version++;

            return m_count;
        }

        /// <summary>
        ///        Adds the elements of a <see cref="BaseValidator"/> array to the current <c>BaseValidatorCollection</c>.
        /// </summary>
        /// <param name="x">The <see cref="BaseValidator"/> array whose elements should be added to the end of the <c>BaseValidatorCollection</c>.</param>
        /// <returns>The new <see cref="BaseValidatorCollection.Count"/> of the <c>BaseValidatorCollection</c>.</returns>
        public virtual int AddRange(BaseValidator[] x)
        {
            if (m_count + x.Length >= m_array.Length)
                EnsureCapacity(m_count + x.Length);

            Array.Copy(x, 0, m_array, m_count, x.Length);
            m_count += x.Length;
            m_version++;

            return m_count;
        }

        /// <summary>
        ///        Sets the capacity to the actual number of elements.
        /// </summary>
        public virtual void TrimToSize()
        {
            this.Capacity = m_count;
        }

        #endregion

        #region Implementation (helpers)

        /// <exception cref="ArgumentOutOfRangeException">
        ///        <para><paramref name="index"/> is less than zero</para>
        ///        <para>-or-</para>
        ///        <para><paramref name="index"/> is equal to or greater than <see cref="BaseValidatorCollection.Count"/>.</para>
        /// </exception>
        private void ValidateIndex(int i)
        {
            ValidateIndex(i, false);
        }

        /// <exception cref="ArgumentOutOfRangeException">
        ///        <para><paramref name="index"/> is less than zero</para>
        ///        <para>-or-</para>
        ///        <para><paramref name="index"/> is equal to or greater than <see cref="BaseValidatorCollection.Count"/>.</para>
        /// </exception>
        private void ValidateIndex(int i, bool allowEqualEnd)
        {
            int max = (allowEqualEnd) ? (m_count) : (m_count - 1);
            if (i < 0 || i > max)
                throw new System.ArgumentOutOfRangeException("Index was out of range.  Must be non-negative and less than the size of the collection.", (object)i, "Specified argument was out of the range of valid values.");
        }

        private void EnsureCapacity(int min)
        {
            int newCapacity = ((m_array.Length == 0) ? DEFAULT_CAPACITY : m_array.Length * 2);
            if (newCapacity < min)
                newCapacity = min;

            this.Capacity = newCapacity;
        }

        #endregion

        #region Implementation (ICollection)

        void ICollection.CopyTo(Array array, int start)
        {
            this.CopyTo((BaseValidator[])array, start);
        }

        #endregion

        #region Implementation (IList)

        object IList.this[int i]
        {
            get { return (object)this[i]; }
            set { this[i] = (BaseValidator)value; }
        }

        int IList.Add(object x)
        {
            return this.Add((BaseValidator)x);
        }

        bool IList.Contains(object x)
        {
            return this.Contains((BaseValidator)x);
        }

        int IList.IndexOf(object x)
        {
            return this.IndexOf((BaseValidator)x);
        }

        void IList.Insert(int pos, object x)
        {
            this.Insert(pos, (BaseValidator)x);
        }

        void IList.Remove(object x)
        {
            this.Remove((BaseValidator)x);
        }

        void IList.RemoveAt(int pos)
        {
            this.RemoveAt(pos);
        }

        #endregion

        #region Implementation (IEnumerable)

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)(this.GetEnumerator());
        }

        #endregion

        #region Nested enumerator class
        /// <summary>
        ///        Supports simple iteration over a <see cref="BaseValidatorCollection"/>.
        /// </summary>
        private class Enumerator : IEnumerator, IBaseValidatorCollectionEnumerator
        {
            #region Implementation (data)

            private BaseValidatorCollection m_collection;
            private int m_index;
            private int m_version;

            #endregion

            #region Construction

            /// <summary>
            ///        Initializes a new instance of the <c>Enumerator</c> class.
            /// </summary>
            /// <param name="tc"></param>
            internal Enumerator(BaseValidatorCollection tc)
            {
                m_collection = tc;
                m_index = -1;
                m_version = tc.m_version;
            }

            #endregion

            #region Operations (type-safe IEnumerator)

            /// <summary>
            ///        Gets the current element in the collection.
            /// </summary>
            public BaseValidator Current
            {
                get { return m_collection[m_index]; }
            }

            /// <summary>
            ///        Advances the enumerator to the next element in the collection.
            /// </summary>
            /// <exception cref="InvalidOperationException">
            ///        The collection was modified after the enumerator was created.
            /// </exception>
            /// <returns>
            ///        <c>true</c> if the enumerator was successfully advanced to the next element; 
            ///        <c>false</c> if the enumerator has passed the end of the collection.
            /// </returns>
            public bool MoveNext()
            {
                if (m_version != m_collection.m_version)
                    throw new System.InvalidOperationException("Collection was modified; enumeration operation may not execute.");

                ++m_index;
                return (m_index < m_collection.Count) ? true : false;
            }

            /// <summary>
            ///        Sets the enumerator to its initial position, before the first element in the collection.
            /// </summary>
            public void Reset()
            {
                m_index = -1;
            }
            #endregion

            #region Implementation (IEnumerator)

            object IEnumerator.Current
            {
                get { return (object)(this.Current); }
            }

            #endregion
        }
        #endregion

        #region Nested Syncronized Wrapper class
        private class SyncBaseValidatorCollection : BaseValidatorCollection
        {
            #region Implementation (data)
            private BaseValidatorCollection m_collection;
            private object m_root;
            #endregion

            #region Construction
            internal SyncBaseValidatorCollection(BaseValidatorCollection list)
            {
                m_root = list.SyncRoot;
                m_collection = list;
            }
            #endregion

            #region Type-safe ICollection
            public override void CopyTo(BaseValidator[] array)
            {
                lock (this.m_root)
                    m_collection.CopyTo(array);
            }

            public override void CopyTo(BaseValidator[] array, int start)
            {
                lock (this.m_root)
                    m_collection.CopyTo(array, start);
            }
            public override int Count
            {
                get
                {
                    lock (this.m_root)
                        return m_collection.Count;
                }
            }

            public override bool IsSynchronized
            {
                get { return true; }
            }

            public override object SyncRoot
            {
                get { return this.m_root; }
            }
            #endregion

            #region Type-safe IList
            public override BaseValidator this[int i]
            {
                get
                {
                    lock (this.m_root)
                        return m_collection[i];
                }
                set
                {
                    lock (this.m_root)
                        m_collection[i] = value;
                }
            }

            public override int Add(BaseValidator x)
            {
                lock (this.m_root)
                    return m_collection.Add(x);
            }

            public override void Clear()
            {
                lock (this.m_root)
                    m_collection.Clear();
            }

            public override bool Contains(BaseValidator x)
            {
                lock (this.m_root)
                    return m_collection.Contains(x);
            }

            public override int IndexOf(BaseValidator x)
            {
                lock (this.m_root)
                    return m_collection.IndexOf(x);
            }

            public override void Insert(int pos, BaseValidator x)
            {
                lock (this.m_root)
                    m_collection.Insert(pos, x);
            }

            public override void Remove(BaseValidator x)
            {
                lock (this.m_root)
                    m_collection.Remove(x);
            }

            public override void RemoveAt(int pos)
            {
                lock (this.m_root)
                    m_collection.RemoveAt(pos);
            }

            public override bool IsFixedSize
            {
                get { return m_collection.IsFixedSize; }
            }

            public override bool IsReadOnly
            {
                get { return m_collection.IsReadOnly; }
            }
            #endregion

            #region Type-safe IEnumerable
            public override IBaseValidatorCollectionEnumerator GetEnumerator()
            {
                lock (m_root)
                    return m_collection.GetEnumerator();
            }
            #endregion

            #region Public Helpers
            // (just to mimic some nice features of ArrayList)
            public override int Capacity
            {
                get
                {
                    lock (this.m_root)
                        return m_collection.Capacity;
                }

                set
                {
                    lock (this.m_root)
                        m_collection.Capacity = value;
                }
            }

            public override int AddRange(BaseValidatorCollection x)
            {
                lock (this.m_root)
                    return m_collection.AddRange(x);
            }

            public override int AddRange(BaseValidator[] x)
            {
                lock (this.m_root)
                    return m_collection.AddRange(x);
            }
            #endregion
        }
        #endregion

        #region Nested Read Only Wrapper class
        private class ReadOnlyBaseValidatorCollection : BaseValidatorCollection
        {
            #region Implementation (data)
            private BaseValidatorCollection m_collection;
            #endregion

            #region Construction
            internal ReadOnlyBaseValidatorCollection(BaseValidatorCollection list)
            {
                m_collection = list;
            }
            #endregion

            #region Type-safe ICollection
            public override void CopyTo(BaseValidator[] array)
            {
                m_collection.CopyTo(array);
            }

            public override void CopyTo(BaseValidator[] array, int start)
            {
                m_collection.CopyTo(array, start);
            }
            public override int Count
            {
                get { return m_collection.Count; }
            }

            public override bool IsSynchronized
            {
                get { return m_collection.IsSynchronized; }
            }

            public override object SyncRoot
            {
                get { return this.m_collection.SyncRoot; }
            }
            #endregion

            #region Type-safe IList
            public override BaseValidator this[int i]
            {
                get { return m_collection[i]; }
                set { throw new NotSupportedException("This is a Read Only Collection and can not be modified"); }
            }

            public override int Add(BaseValidator x)
            {
                throw new NotSupportedException("This is a Read Only Collection and can not be modified");
            }

            public override void Clear()
            {
                throw new NotSupportedException("This is a Read Only Collection and can not be modified");
            }

            public override bool Contains(BaseValidator x)
            {
                return m_collection.Contains(x);
            }

            public override int IndexOf(BaseValidator x)
            {
                return m_collection.IndexOf(x);
            }

            public override void Insert(int pos, BaseValidator x)
            {
                throw new NotSupportedException("This is a Read Only Collection and can not be modified");
            }

            public override void Remove(BaseValidator x)
            {
                throw new NotSupportedException("This is a Read Only Collection and can not be modified");
            }

            public override void RemoveAt(int pos)
            {
                throw new NotSupportedException("This is a Read Only Collection and can not be modified");
            }

            public override bool IsFixedSize
            {
                get { return true; }
            }

            public override bool IsReadOnly
            {
                get { return true; }
            }
            #endregion

            #region Type-safe IEnumerable
            public override IBaseValidatorCollectionEnumerator GetEnumerator()
            {
                return m_collection.GetEnumerator();
            }
            #endregion

            #region Public Helpers
            // (just to mimic some nice features of ArrayList)
            public override int Capacity
            {
                get { return m_collection.Capacity; }

                set { throw new NotSupportedException("This is a Read Only Collection and can not be modified"); }
            }

            public override int AddRange(BaseValidatorCollection x)
            {
                throw new NotSupportedException("This is a Read Only Collection and can not be modified");
            }

            public override int AddRange(BaseValidator[] x)
            {
                throw new NotSupportedException("This is a Read Only Collection and can not be modified");
            }
            #endregion
        }
        #endregion
    }

    #endregion

    #region BaseValidatorManager

    /// <summary>
    /// Manages in in-memory list of BaseValidators.
    /// </summary>
    /// <remarks>
    /// Each BaseValidator component registers itself with the BaseValidator by calling
    /// <c>RegisterBaseValidator</c>.
    /// </remarks>
    public partial class BaseValidatorManager
    {
        /// <summary>
        /// </summary>
        /// <remarks>
        /// </remarks>
        internal static BaseValidatorCollection GetRegisteredBaseValidators()
        {
            return _registeredBaseValidators;
        }

        internal static void RegisterBaseValidator(BaseValidator validator)
        {
            // Add this validator to the list of registered validators
            _registeredBaseValidators.Add(validator);
        }

        internal static void DeRegisterBaseValidator(BaseValidator validator)
        {
            // Remove this validator from the list of registered validators
            _registeredBaseValidators.Remove(validator);
        }

        private static BaseValidatorCollection _registeredBaseValidators = new BaseValidatorCollection();
    }
    #endregion

    #region ContainerValidator

    /// <summary>
    /// Performs validation on the specified container.
    /// </summary>
    /// <remarks>
    /// Container-level validation calls <c>Validate</c> on each validation control within the specified container, 
    /// whether the <c>ContainerToValidate</c> is the direct or indirect parent of the control being validated.
    /// <c>IsValid</c> returns whether the most recent validation was a success or not.<br/><br/>
    /// ContainerValidator offers both instance and static implementions of <c>Validate</c> and <c>IsValid</c>.<br/><br/>
    /// The following controls are valid containers:
    /// <list type="bullet">
    /// <item>Form</item>
    /// <item>GroupBox</item>
    /// <item>Panel</item>
    /// <item>TabControl</item>
    /// <item>TabPage</item>
    /// <item>UserControl</item>
    /// </list>
    /// </remarks>
    [
        ToolboxBitmap(typeof(ContainerValidator), "ContainerValidator.ico")
    ]
    public partial class ContainerValidator : Component, ISupportInitialize
    {
        #region ISupportInitialize

        void ISupportInitialize.BeginInit() { }
        void ISupportInitialize.EndInit()
        {
            // Ensure HostingForm is set as this provides a crucial association with form-level validation,
            // via ContainerValidator
            if (!this.DesignMode)
            {
                if (_containerToValidate == null)
                {
                    throw new Exception("ContainerToValidate cannot be null.");
                }
            }
        }

        #endregion

        /// <summary>
        /// Gets a value that indicates whether the specified container passes validation.
        /// </summary>
        /// <remarks>
        /// The value that indicates whether the specified container passes validation.
        /// </remarks>
        /// <param name="containerToValidate">The validated container control.</param>
        public static bool IsValid(Control containerToValidate)
        {
            return ContainerValidator.IsValid(containerToValidate, ValidationDepth.All);
        }

        /// <summary>
        /// Gets a value that indicates whether the specified container passes validation.
        /// </summary>
        /// <remarks>
        /// The value that indicates whether the specified container passes validation. 
        /// Uses a validation depth of <c>ValidationDepth.All</c>.
        /// </remarks>
        /// <param name="containerToValidate">The validated container control.</param>
        /// <param name="validationDepth">Validate direct and/or indirect children.</param>
        public static bool IsValid(Control containerToValidate, ValidationDepth validationDepth)
        {
            // Check if every validator control (on the specified form) IsValid
            // If one of them is false, the container is false
            foreach (BaseValidator validator in BaseValidatorManager.GetRegisteredBaseValidators())
            {
                if (IsParent(containerToValidate, validator.ControlToValidate, validationDepth))
                {
                    validator.Validate();
                    if (!validator.IsValid) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Validates the specified container.
        /// </summary>
        /// <remarks>
        /// Container-level validation is performed by calling <c>Validate</c> on each validation control 
        /// within the specified container. Uses a validation depth of <c>ValidationDepth.All</c>.
        /// </remarks>
        /// <param name="containerToValidate">The container control to validate.</param>
        public static void Validate(Control containerToValidate)
        {
            ContainerValidator.Validate(containerToValidate, ValidationDepth.All);
        }

        /// <summary>
        /// Validates the specified container.
        /// </summary>
        /// <remarks>
        /// Container-level validation is performed by calling <c>Validate</c> on each validation control 
        /// within the specified container.
        /// </remarks>
        /// <param name="containerToValidate">The container control to validate.</param>
        /// <param name="validationDepth">Validate direct and/or indirect children.</param>
        public static void Validate(Control containerToValidate, ValidationDepth validationDepth)
        {
            // Validate
            //Control   control = null;
            BaseValidator firstInTabOrder = null;
            foreach (BaseValidator validator in BaseValidatorManager.GetRegisteredBaseValidators())
            {
                // Only validate BaseValidators hosted by the container I reference
                //if( validator.ControlToValidate.Parent != _containerToValidate ) continue;
                if (IsParent(containerToValidate, validator.ControlToValidate, validationDepth))
                {
                    // Validate control
                    validator.Validate();

                    // Set focus on the control it its invalid and the earliest invalid
                    // control in the tab order
                    if (!validator.IsValid)
                    {
                        if ((firstInTabOrder == null) ||
                            (decimal.Parse(firstInTabOrder.FlattenedTabIndex) > decimal.Parse(validator.FlattenedTabIndex)))
                        {
                            firstInTabOrder = validator;
                        }
                        if (firstInTabOrder != null) firstInTabOrder.ControlToValidate.Focus();
                    }
                }
            }
        }
        /// <summary>
        /// Initializes a new instance of the ContainerValidator class.
        /// </summary>
        public ContainerValidator() : base() { }

        /// <summary>
        /// Initializes a new instance of the ContainerValidator class, with the specified container to validate.
        /// </summary>
        /// <param name="containerToValidate">The containerToValidate that this ContainerValidator instance is associated with.</param>
        public ContainerValidator(Control containerToValidate)
        {
            this.ContainerToValidate = containerToValidate;
        }

        /// <summary>
        /// Gets or sets the input control to validate.
        /// </summary>
        /// <value>
        /// The input control to validate. The default value is <c>null</c>.
        /// </value>
        [
            Category("Behaviour"),
            TypeConverter(typeof(ContainerControlConverter)),
            DefaultValue(null),
            Description("Gets or sets the input control to validate.")
        ]
        public Control ContainerToValidate
        {
            get { return _containerToValidate; }
            set
            {
                // Set container to validate
                _containerToValidate = value;
            }
        }

        /// <summary>
        /// Gets or sets the level of validation applied to <c>ContainerToValidate</c> using the <c>ValidationDepth</c> enumeration. 
        /// <c>ValidationDepth.All</c> is the default and validates those <c>BaseValidators</c> whose direct or indirect 
        /// parent is <c>ContainerToValidate</c>, while <c>ValidationDepth.ContainerOnly</c> validates 
        /// only the <c>BaseValidators</c> whose direct parent is <c>ContainerToValidate</c>.
        /// </summary>
        /// <value>
        /// The value that indicates whether <c>ContainerToValidate</c> passes validation.
        /// </value>
        [
            Category("Behavior"),
            DefaultValue(ValidationDepth.All),
            Description(@"Gets or sets the level of validation applied to ContainerToValidate using the ValidationDepth enumeration.")
        ]
        public ValidationDepth ValidationDepth
        {
            get { return _validationDepth; }
            set { _validationDepth = value; }
        }
        /// <summary>
        /// Gets a value that indicates whether the associated <c>ContainerToValidate</c> passes validation.
        /// </summary>
        /// <value>
        /// The value that indicates whether <c>ContainerToValidate</c> passes validation.
        /// </value>
        public bool IsValid() { return ContainerValidator.IsValid(_containerToValidate, _validationDepth); }
        /// <summary>
        /// Validates the <c>ContainerToValidate</c>.
        /// </summary>
        /// <remarks>
        /// Container-level validation is performed by calling <c>Validate</c> on each validation control 
        /// within <c>ContainerToValidate</c>.
        /// </remarks>
        public void Validate() { ContainerValidator.Validate(_containerToValidate, _validationDepth); }

        private static bool IsParent(Control parent, Control child, ValidationDepth validationDepth)
        {
            if (validationDepth == ValidationDepth.ContainerOnly)
            {
                return (child.Parent == parent);
            }
            else
            {
                Control current = child;
                while (current != null)
                {
                    if (current == parent) return true;

                    current = current.Parent;
                }
                return false;
            }
        }


        Control _containerToValidate = null;
        ValidationDepth _validationDepth = ValidationDepth.All;
    }

    #endregion

    #region BaseValidator

    /// <summary>
    /// Serves as the abstract base class for validation controls.
    /// </summary>
    /// <remarks>
    /// The <c>BaseValidator</c> class provides the core implementation for all validation controls.
    /// Validation occurs when the associated control issues a Validating event, which the base
    /// validator registers itself with with the <c>ControlToValidate</c> property is set.<br/><br/>
    /// The <c>BaseValidator</c> also registers itself with <c>ContainerValidator</c> if it successfully
    /// initializes, which is determined by whether the <c>ControlToValidate</c> property has been set.<br/><br/>
    /// <b>Note</b>  No validation occurs until the first time a <c>ControlToValidate</c> has been altered
    /// from it's initial value.
    /// </remarks>
    public abstract class BaseValidator : Component, ISupportInitialize
    {
        #region ISupportInitialize

        void ISupportInitialize.BeginInit() { }
        void ISupportInitialize.EndInit()
        {
            // Ensure ControlToValidate is set, as this component does not function
            // without one, and it might be useful for the developer to know.  Also, the 
            // Web Validation controls use an analogous notification technique.
            if (!this.DesignMode)
            {
                if (_controlToValidate == null)
                {
                    throw new Exception("The ControlToValidate property cannot be null.");
                }
            }
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the BaseValidator class.
        /// </summary>
        public BaseValidator()
        {
            // Allow this validator instance to be accessed from Validators
            BaseValidatorManager.RegisterBaseValidator(this);
        }

        /// <summary>
        /// Initializes a new instance of the BaseValidator class.
        /// </summary>
        public BaseValidator(Control controlToValidate)
        {
            if (controlToValidate == null)
            {
                throw new Exception("The ControlToValidate property cannot be null.");
            }

            _controlToValidate = controlToValidate;

            // Allow this validator instance to be accessed from Validators
            BaseValidatorManager.RegisterBaseValidator(this);
        }

        /// <summary>
        /// Gets a value that indicates whether the associated input control passes validation.
        /// </summary>
        /// <value>
        /// <c>true</c> if the associated input control passes validation, <c>false</c> otherwise.
        /// </value>
        [
            Browsable(false),
            DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
        ]
        public bool IsValid
        {
            get { return _isValid; }
        }

        /// <summary>
        /// Gets or sets the text for the error message.
        /// </summary>
        /// <value>
        /// The error message displayed when validation fails. The default value is <c>String.Empty</c>.
        /// </value>
        [
            Category("Appearance"),
            DefaultValue(""),
            Description("Gets or sets the text for the error message.")
        ]
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { _errorMessage = value; }
        }

        /// <summary>
        /// Gets or sets the control to validate.
        /// </summary>
        /// <value>
        /// The control to validate. The default value is <c>null</c>.
        /// </value>
        [
            Category("Behaviour"),
            TypeConverter(typeof(ValidatableControlConverter)),
            DefaultValue(null),
            Description("Gets or sets the input control to validate.")
        ]
        public Control ControlToValidate
        {
            get { return _controlToValidate; }
            set
            {
                // Set control to validate
                _controlToValidate = value;

                // Register hooks with ControlToValidate
                if ((_controlToValidate != null) && (!DesignMode))
                {
                    // Register ControlToValidate's validating event
                    _controlToValidate.Validating += new CancelEventHandler(ControlToValidate_Validating);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the validation control will validate automatically, or
        /// only validate only when Validate() is called.
        /// </summary>
        /// <value>
        /// <c>true</c> if the control is automatically validated. <c>false</c> otherwise.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(true),
            Description("Gets or sets whether the validation control will validate automatically, or only validate only when Validate() is called.")
        ]
        public bool Automatic
        {
            get { return _automatic; }
            set { _automatic = value; }
        }

        /// <summary>
        /// Gets or sets the amount of extra space to leave between the specified control and the error icon.
        /// </summary>
        /// <value>
        /// The amount of extra space to leave between the specified control and the error icon.
        /// </value>
        [
            Category("Appearance"),
            DefaultValue(0),
            Description("Gets or sets the amount of extra space to leave between the specified control and the error icon.")
        ]
        public int IconPadding
        {
            get { return _iconPadding; }
            set { _iconPadding = value; }
        }

        /// <summary>
        /// Gets or sets the Icon that is displayed next to a control when an error description 
        /// string has been set for the control.
        /// </summary>
        /// <value>
        /// The Icon that is displayed next to a control when an error description 
        /// string has been set for the control.
        /// </value>
        [
            Category("Appearance"),
            Description("Gets or sets the Icon that is displayed next to a control when an error description string has been set for the control.")
        ]
        public Icon Icon
        {
            get { return _icon; }
            set { _icon = value; }
        }

        /// <summary>
        /// Gets or sets the location where the error icon should be placed in relation to the control.
        /// </summary>
        /// <value>
        /// The location where the error icon should be placed in relation to the control.
        /// </value>
        [
            Category("Appearance"),
            DefaultValue(ErrorIconAlignment.MiddleRight),
            Description("Gets or sets the location where the error icon should be placed in relation to the control.")
        ]
        public ErrorIconAlignment IconAlignment
        {
            get { return _iconAlignment; }
            set { _iconAlignment = value; }
        }

        /// <summary>
        /// Performs validation on the associated input control and updates the <c>IsValid</c> property.
        /// </summary>
        /// <value>
        /// The input control to validate. The default value is <c>null</c>.
        /// </value>
        public void Validate()
        {
            // Validate control
            string errorMessage = "";
            _isValid = EvaluateIsValid();

            // If control isn't valid, set up the error provider behaviour to display an error
            if (!_isValid)
            {
                _errorProvider.Icon = _icon;
                _errorProvider.SetIconAlignment(_controlToValidate, _iconAlignment);
                _errorProvider.SetIconPadding(_controlToValidate, _iconPadding);
                _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;

                errorMessage = _errorMessage;
            }
            _errorProvider.SetError(_controlToValidate, errorMessage);
        }

        /// <summary>
        /// When overridden in a derived class, this method contains the code to 
        /// determine whether the value in the input control is valid.
        /// </summary>
        /// <value>
        /// <c>true</c> if the associated input control passes validation, <c>false</c> otherwise.
        /// </value>
        protected abstract bool EvaluateIsValid();

        internal string FlattenedTabIndex
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                Control current = _controlToValidate;
                while (current != null)
                {
                    string tabIndex = current.TabIndex.ToString();
                    sb.Insert(0, tabIndex);
                    current = current.Parent;
                }
                sb.Insert(0, "0.");
                return sb.ToString();
            }
        }
        private void ControlToValidate_Validating(object sender, CancelEventArgs e)
        {
            // Validate on control's Validating event, if Automatic mode
            if (_automatic)
            {
                Validate();
            }
        }


        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            // Deregister
            BaseValidatorManager.DeRegisterBaseValidator(this);

            // Be nice
            base.Dispose(disposing);

            // Don't allow dispose to be called again
            GC.SuppressFinalize(this);
        }

        private int _iconPadding = 0;
        private bool _isValid = false;
        private bool _automatic = true;
        private Icon _icon = new Icon(typeof(ErrorProvider), "Error.ico");
        private string _errorMessage = "";
        private ErrorProvider _errorProvider = new ErrorProvider();
        private Control _controlToValidate = null;
        private ErrorIconAlignment _iconAlignment = ErrorIconAlignment.MiddleRight;
    }

    #endregion

    #region BaseCompareValidator

    /// <summary>
    /// Serves as the abstract base class for validation controls requiring type-specific validation,
    /// formatting, comparison and conversion.
    /// </summary>
    /// <remarks>
    /// The <c>BaseCompareValidator</c> class provides the core implementation for compare-style
    /// validation controls, including both the <c>RangeValidator</c> and <c>CompareValidator</c>.
    /// </remarks>
    public abstract class BaseCompareValidator : BaseValidator
    {
        /// <summary>
        /// Gets or sets what type-checking will be applied to the validation.
        /// </summary>
        /// <value>
        /// What type-checking will be applied to the validation, default is <c>ValidationDataType.String</c>.
        /// </value>    
        [
            Category("Behaviour"),
            DefaultValue(ValidationDataType.String)
        ]
        public ValidationDataType Type
        {
            get { return _type; }
            set
            {
                _type = value;
            }
        }

        /// <summary>
        /// Gets a TypeConverter capable of performing conversions to and from the type, specified by <c>ValidationDataType</c>.
        /// </summary>
        /// <value>
        /// A TypeConverter capable of performing conversions to and from the type, specified by <c>ValidationDataType</c>.
        /// </value>    
        protected TypeConverter TypeConverter
        {
            get { return TypeDescriptor.GetConverter(System.Type.GetType(_typeTable[(int)_type])); }
        }

        /// <summary>
        /// Returns a value indicating whether the string <c>value</c> can be converted to the type specified by <c>Type</c>.
        /// </summary>
        /// <param name="value">String value being tested.</param>
        /// <returns><c>true</c> if value can be converted, <c>false</c> otherwise.</returns>
        protected bool CanConvert(string value)
        {
            try
            {
                TypeConverter _converter = TypeDescriptor.GetConverter(System.Type.GetType(_typeTable[(int)_type]));
                _converter.ConvertFrom(value);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Returns a formatted string.
        /// </summary>
        /// <remarks><c>Format</c> is used to clean a string from any unnecessary UI artifacts before converting
        /// it from the type specified by <c>Type</c> to the specified <c>System</c>type.  For example, a <c>Currency</c>
        /// type is internally converted to <c>System.Decimal</c>.  From a UI point of view, it is perfectly valid to include 
        /// <b>$</b> and <b>.</b> characters in a <c>Currency</c> field on a UI, but such a field cannot be converted to 
        /// <c>System.Decimal</c>.  <c>Format</c> is used to strip any characters that won't make the conversion from UI <c>Type</c>
        /// to internal <c>System</c> type.<br/><br/>The following table shows what ValidationDataTypes are converted to
        /// what internal <c>System</c> types.
        /// <list type="table">
        /// <listheader>
        /// <term>ValidationDataType</term><description><c>System</c> Type</description>
        /// </listheader>
        /// <item><term>Currency</term><description>System.Decimal</description></item>
        /// <item><term>Date</term><description>System.DateTime</description></item>
        /// <item><term>Double</term><description>System.Double</description></item>
        /// <item><term>Integer</term><description>System.Int32</description></item>
        /// <item><term>String</term><description>System.String</description></item>
        /// </list>
        /// </remarks>
        /// <param name="value">String value of <c>Type</c>to be formatted prior to conversion to an internal
        /// <c>System</c> type representation.</param>
        /// <returns><c>true</c> if value can be converted, <c>false</c> otherwise.</returns>
        protected string Format(string value)
        {
            // If currency
            if (_type == ValidationDataType.Currency)
            {
                // Convert to decimal format ie remove currency formatting characters
                return Regex.Replace(value, "[$ .]", "");
            }

            return value;
        }

        private ValidationDataType _type = ValidationDataType.String;
        private string[] _typeTable = new string[5] {"System.Decimal", 
                                                                    "System.DateTime",
                                                                    "System.Double",
                                                                    "System.Int32",
                                                                    "System.String"};
    }

    #endregion

    #region RequiredFieldValidator

    /// <summary>
    /// Makes the associated input control a required field.
    /// </summary>
    /// <remarks>
    /// Use this control to make an input control a required field. The input control fails validation if 
    /// its value does not change from the <c>InitialValue</c> property upon losing focus.
    /// </remarks>
    [
        ToolboxBitmap(typeof(RequiredFieldValidator), "RequiredFieldValidator.ico")
    ]
    public partial class RequiredFieldValidator : BaseValidator
    {
        /// <summary>
        /// Gets or sets the initial value of the associated input control.
        /// </summary>
        /// <value>
        /// The initial value of the associated input control.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(""),
            Description("If the entered value is different than the initial value, the control to validate is considered invalid.")
        ]
        public string InitialValue
        {
            get { return _initialValue; }
            set { _initialValue = value; }
        }

        /// <summary>
        /// This member overrides <c>BaseValidator.EvaluateIsValid</c>.
        /// </summary>
        protected override bool EvaluateIsValid()
        {
            return (ControlToValidate.Text.Trim() != _initialValue.Trim());
        }

        private string _initialValue = "";
    }

    #endregion

    #region RegularExpressionValidator

    /// <summary>
    /// Validates whether the value of an associated input control matches the pattern specified 
    /// by a regular expression.
    /// </summary>
    /// <remarks>
    /// The <c>RegularExpressionValidator</c> control checks whether the value of an input control matches 
    /// a pattern defined by a regular expression.  Validation succeeds if the input control's value
    /// is empty.
    /// </remarks>
    [
        ToolboxBitmap(typeof(RegularExpressionValidator), "RegularExpressionValidator.ico")
    ]
    public partial class RegularExpressionValidator : BaseValidator
    {
        /// <summary>
        /// Gets or sets a the regular expression used to validated the associated input control.
        /// </summary>
        /// <value>
        /// The regular expression used to validated the associated input control.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(""),
            Editor(typeof(RegexTypeEditor), typeof(System.Drawing.Design.UITypeEditor)),
            Description("Gets or sets a the regular expression used to validated the associated input control.")
        ]
        public string ValidationExpression
        {
            get { return _validationExpression; }
            set { _validationExpression = value; }
        }

        /// <summary>
        /// Gets or sets whether this control is required.
        /// </summary>
        /// <value>
        /// <b>true</b> if this control is required; otherwise, <b>false</b>.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(true),
            Description("Gets or sets whether this control is required.")
        ]
        public bool Required
        {
            get { return _required; }
            set { _required = value; }
        }

        /// <summary>
        /// This member overrides <c>BaseValidator.EvaluateIsValid</c>.
        /// </summary>
        protected override bool EvaluateIsValid()
        {
            // Don't validate if empty, unless required
            if (ControlToValidate.Text.Trim() == "") return (!_required);

            // Successful if match matches the entire text of ControlToValidate
            Match match = Regex.Match(ControlToValidate.Text.Trim(), _validationExpression.Trim());
            return (match.Value == ControlToValidate.Text.Trim());
        }

        private bool _required = true;
        private string _validationExpression = "";
    }

    #endregion

    #region RangeValidator

    /// <summary>
    /// Checks whether the value of an input control is within a specified range of values.
    /// </summary>
    /// <remarks>
    /// The <c>RangeValidator</c> control checks whether the value of an input control falls
    /// within the range specified by <c>MinimumValue</c> and <c>MaximumValue</c>.  Both of these
    /// properties must be provided for the control to run.  The range comparison is performed using
    /// a specific type, specified by <c>BaseCompareValidator.Type</c>.  The range comparison will
    /// succeed if the value being compared is greater-than-or-equal-to <c>MinimumValue</c> and 
    /// less-than-or-equal-to <c>MaximumValue</c>.<br/><br/>
    /// <b>Note</b>  If the input control's value does not match the type specified by 
    /// <c>BaseCompareValidator.Type</c> the validation fails.
    /// </remarks>
    [
        ToolboxBitmap(typeof(RangeValidator), "RangeValidator.ico")
    ]
    public partial class RangeValidator : BaseCompareValidator
    {
        /// <summary>
        /// Gets or sets a the minimum value of the range being tested.
        /// </summary>
        /// <value>
        /// The minimum value of the range being tested.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(""),
            Description("Gets or sets a the minimum value of the range being tested.")
        ]
        public string MinimumValue
        {
            get { return _minimumValue; }
            set { _minimumValue = value; }
        }

        /// <summary>
        /// Gets or sets a the maximum value of the range being tested.
        /// </summary>
        /// <value>
        /// The maximum value of the range being tested.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(""),
            Description("Gets or sets a the maximum value of the range being tested.")
        ]
        public string MaximumValue
        {
            get { return _maximumValue; }
            set { _maximumValue = value; }
        }

        /// <summary>
        /// Gets or sets whether this control is required.
        /// </summary>
        /// <value>
        /// <b>true</b> if this control is required; otherwise, <b>false</b>.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(true),
            Description("Gets or sets whether this control is required.")
        ]
        public bool Required
        {
            get { return _required; }
            set { _required = value; }
        }

        /// <summary>
        /// This member overrides <c>BaseValidator.EvaluateIsValid</c>.
        /// </summary>
        protected override bool EvaluateIsValid()
        {
            // Don't validate if empty, unless required
            if (ControlToValidate.Text.Trim() == "") return (!_required);

            // Validate and convert Minimum
            if (_minimumValue.Trim() == "") throw new Exception("MinimumValue must be provided.");
            string formattedMinimumValue = Format(_minimumValue.Trim());
            if (!CanConvert(formattedMinimumValue)) throw new Exception("MinimumValue cannot be converted to the specified Type.");
            object minimum = TypeConverter.ConvertFrom(formattedMinimumValue);

            // Validate and convert Maximum
            if (_maximumValue.Trim() == "") throw new Exception("MaximumValue must be provided.");
            string formattedMaximumValue = Format(_maximumValue.Trim());
            if (!CanConvert(formattedMaximumValue)) throw new Exception("MaximumValue cannot be converted to the specified Type.");
            object maximum = TypeConverter.ConvertFrom(formattedMaximumValue);

            // Check minimum <= maximum
            if (Comparer.Default.Compare(minimum, maximum) > 0) throw new Exception("MinimumValue cannot be greater than MaximumValue.");

            // Check and convert ControlToValue
            string formattedValue = Format(ControlToValidate.Text.Trim());
            if (!CanConvert(formattedValue)) return false;
            object value = TypeConverter.ConvertFrom(formattedValue);

            // Is value in range (minimum <= value <= maximum)
            return ((Comparer.Default.Compare(minimum, value) <= 0) &&
                    (Comparer.Default.Compare(value, maximum) <= 0));
        }

        private bool _required = true;
        private string _minimumValue = "";
        private string _maximumValue = "";
    }

    #endregion

    #region CompareValidator

    /// <summary>
    /// Compares the value entered by the user into an input control with the value entered 
    /// into another input control or a constant value.
    /// </summary>
    /// <remarks>
    /// The <c>CompareValidator</c> compares the input control's value with a value specified by
    /// another control (<c>ControlToCompare</c>) or a constant value (<c>ValueToCompare</c>).  Either
    /// <c>ControlToCompare</c> or <c>ValueToCompare</c> must be provided and, if both are, 
    /// <c>ControlToCompare</c> is compared against.<br/><br/>
    /// <b>Note</b>  If the input control's value does not match the type specified by 
    /// <c>BaseCompareValidator.Type</c> the validation fails.
    /// </remarks>
    [
        ToolboxBitmap(typeof(CompareValidator), "CompareValidator.ico")
    ]
    public partial class CompareValidator : BaseCompareValidator
    {
        /// <summary>
        /// Gets or sets the control to compare the input control's value against.
        /// </summary>
        /// <value>
        /// The control to compare the input control's value against.
        /// </value>
        [
            Category("Behaviour"),
            TypeConverter(typeof(ValidatableControlConverter)),
            DefaultValue(null),
            Description("Gets or sets the control to compare the input control's value against.")
        ]
        public Control ControlToCompare
        {
            get { return _controlToCompare; }
            set { _controlToCompare = value; }
        }

        /// <summary>
        /// Gets or sets the type of comparison being performed.
        /// </summary>
        /// <value>
        /// The type of comparison being performed.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(null),
            Description("Gets or sets the type of comparison being performed.")
        ]
        public ValidationCompareOperator Operator
        {
            get { return _operator; }
            set { _operator = value; }
        }

        /// <summary>
        /// Gets or sets the value to compare the input control's value against.
        /// </summary>
        /// <value>
        /// The value to compare the input control's value against.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(""),
            Description("Gets or sets the value to compare the input control's value against.")
        ]
        public string ValueToCompare
        {
            get { return _valueToCompare; }
            set { _valueToCompare = value; }
        }

        /// <summary>
        /// Gets or sets whether this control is required.
        /// </summary>
        /// <value>
        /// <b>true</b> if this control is required; otherwise, <b>false</b>.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(true),
            Description("Gets or sets whether this control is required.")
        ]
        public bool Required
        {
            get { return _required; }
            set { _required = value; }
        }

        /// <summary>
        /// This member overrides <c>BaseValidator.EvaluateIsValid</c>.
        /// </summary>    
        protected override bool EvaluateIsValid()
        {
            // Don't validate if empty, unless required
            if (ControlToValidate.Text.Trim() == "") return (!_required);

            // Can't evaluate if missing ControlToCompare and ValueToCompare property values
            if ((_controlToCompare == null) && (_valueToCompare == "")) throw new Exception("The ControlToCompare property cannot be blank.");

            // Validate and convert CompareFrom
            string formattedCompareFrom = Format(ControlToValidate.Text);
            bool canConvertFrom = CanConvert(formattedCompareFrom);
            if (canConvertFrom)
            {
                if (_operator == ValidationCompareOperator.DataTypeCheck) return canConvertFrom;
            }
            else return false;
            object compareFrom = TypeConverter.ConvertFrom(formattedCompareFrom);

            // Validate and convert CompareTo
            string formattedCompareTo = Format(((_controlToCompare != null) ? _controlToCompare.Text : _valueToCompare));
            if (!CanConvert(formattedCompareTo)) throw new Exception("The value you are comparing to cannot be converted to the specified Type.");
            object compareTo = TypeConverter.ConvertFrom(formattedCompareTo);

            // Perform comparison
            int result = Comparer.Default.Compare(compareFrom, compareTo);
            switch (_operator)
            {
                case ValidationCompareOperator.Equal:
                    return (result == 0);

                case ValidationCompareOperator.GreaterThan:
                    return (result > 0);

                case ValidationCompareOperator.GreaterThanEqual:
                    return (result >= 0);

                case ValidationCompareOperator.LessThan:
                    return (result < 0);

                case ValidationCompareOperator.LessThanEqual:
                    return (result <= 0);

                case ValidationCompareOperator.NotEqual:
                    return ((result != 0));

                default: return false;
            }
        }

        private bool _required = true;
        private string _valueToCompare = "";
        private Control _controlToCompare = null;
        private ValidationCompareOperator _operator = ValidationCompareOperator.Equal;
    }

    #endregion

    #region CustomValidator

    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>
    [
        ToolboxBitmap(typeof(CustomValidator), "CustomValidator.ico")
    ]
    public partial class CustomValidator : BaseValidator
    {

        /// <summary>
        /// Gets or sets whether this control is required.
        /// </summary>
        /// <value>
        /// <b>true</b> if this control is required; otherwise, <b>false</b>.
        /// </value>
        [
            Category("Behaviour"),
            DefaultValue(true),
            Description("Gets or sets whether this control is required.")
        ]
        public bool Required
        {
            get { return _required; }
            set { _required = value; }
        }

        public class ValidationCancelEventArgs
        {
            private bool _valid;
            private Control _controlToValidate;

            public ValidationCancelEventArgs(bool valid, Control controlToValidate)
            {
                _valid = valid;
                _controlToValidate = controlToValidate;
            }

            public bool Valid
            {
                get { return _valid; }
                set { _valid = value; }
            }

            public Control ControlToValidate
            {
                get { return _controlToValidate; }
                set { _controlToValidate = value; }
            }
        }
        public delegate void ValidatingEventHandler(object sender, ValidationCancelEventArgs e);
        public event ValidatingEventHandler Validating;
        public void OnValidating(ValidationCancelEventArgs e)
        {
            if (Validating != null) Validating(this, e);
        }

        /// <summary>
        /// This member overrides <c>BaseValidator.EvaluateIsValid</c>.
        /// </summary>
        protected override bool EvaluateIsValid()
        {
            // Don't validate if empty, unless required
            if (ControlToValidate.Text.Trim() == "") return (!_required);

            // Pass validation decision-making to event handler and wait for response
            ValidationCancelEventArgs args = new ValidationCancelEventArgs(false, this.ControlToValidate);
            OnValidating(args);
            return args.Valid;
        }

        private bool _required = true;
    }

    #endregion
}
