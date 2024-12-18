#region License
// Copyright 2004-2024 Castle Project - https://www.castleproject.org/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

namespace Castle.Services.Transaction;

/// <summary>
/// Represents a contract for a resource that can be enlisted within transactions.
/// </summary>
public interface IResource
{
    /// <summary>
    /// Implementors should start the transaction on the underlying resource.
    /// </summary>
    void Start();

    /// <summary>
    /// Implementors should commit the transaction on the underlying resource.
    /// </summary>
    void Commit();

    /// <summary>
    /// Implementors should rollback the transaction on the underlying resource.
    /// </summary>
    void Rollback();
}
