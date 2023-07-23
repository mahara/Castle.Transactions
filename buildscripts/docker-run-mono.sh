#!/bin/bash
# ****************************************************************************
# Copyright 2004-2022 Castle Project - https://www.castleproject.org/
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
# ****************************************************************************


set -e

MONO_TAG=${MONO_TAG:-6.0.0.334}
docker run --rm -v "$PWD":'/project' -w='/project' mono:$MONO_TAG mono "$@"
