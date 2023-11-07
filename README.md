# HugoChecker action

[Hugo](https://gohugo.io) is a great system for generating web pages in no time, but it requires that all information given to it be properly configured.

This action allows us to check our hugo setup before it's even published.

1. Is the structure of the language files correct, are all relevant files found?
2. Do the files contain all required headers?
3. Do the files contain the headers according to the required values?
4. Are the mark down files correct?
5. Does the slug in individual files match the assumed regex?
6. Is the content grammatically and stylistically correct? ChatGpt is used here as support

Example usage:

```yaml
runs:
  steps:
    - name: Check out the Hugo *.md files
      uses: ChrisPrusik/HugoChecker@v3
      with:
        hugo-folder: ${{ github.workspace }}
        chatgpt-api-key: ${{ secrets.CHATGPT_API_KEY }}
```

Arguments:

1. The input `hugo-folder` is the root folder of the Hugo project.
2. The input `chatgpt-api-key` is used for spell check and grammar. It's unecessary if you don't need ChatGPT spell check.

> **NOTE:** 
>
> * The current version of the action operates only on yaml configurations and markdown headers.
> * Language code must be in two letter: en, pl etc.
> * Feel free to send ***PULL REQUESTS*** to this repo: [github.com/ChrisPrusik/HugoChecker](https://github.com/ChrisPrusik/HugoChecker)
> * Project website: [github.com/marketplace/actions/hugochecker](https://github.com/marketplace/actions/hugochecker)

## Root folder

The root folder given as the input `hugo-folder` should also contain hugo the configuration file `config.yaml`. At least, two parameters should be described in that file:

```yaml
languageCode: en
title: Website title
```

## Content folders

In all hugo folders you should put a file `hugo-checker.yaml` with check parameters for the markdown files of the folder. For example:

```yaml
default-language: en
languages:
  - pl
  - en
ignore-files:
  - undone.md
  - undone.pl.md
required-headers:
  - slug
  - title
  - description
  - author
  - date
required-lists:
  series:
    en:
      - Personal experience
      - Existential question
      - Practice
    pl:
      - Osobiste doświadczenie
      - Egzystencjalne pytanie
      - Praktyka
  categories:
    en:
      - For everyone
      - For beginners
      - For advanced
    pl:
      - Dla wszystkich
      - Dla początkujących
      - Dla zaawansowanych
check-header-duplicates:
  - slug
  - title
check-language-structure: yes
check-file-language: yes
check-mark-down: yes
check-spelling: yes
check-slug-regex: yes
pattern-slug-regex: ^[a-z0-9]+(?:-[a-z0-9]+)*$
chat-gpt-spell-check: no
chat-gpt-prompt: >
  Your role is to check the text message provided by the user in the next messages.
  You will have two tasks to done. And result of the task put in an answer as json, 
  see example below:
  {
      "Language": "en",
      "SpellCheck": true
      "Comment": "Everything is ok"
  }
  
  Task 1: Language detection.
  
  Your role is to identify language of the text message provided by the user in the next messages.
  Do not ask any questions - just make a judgement.
  If you are not sure about the language, choose the most probable one.
  Your answer should be only two letter code of the language (ISO 639-1 code), 
  as the first json value (in our example it is "en").

  Task 2: Spellcheck.

  Your role is to check the correctness of the text in terms of style and grammar.
  Do not ask any questions - just make a judgement.
  As an answer in one word "true" - if everything is correct, as second json value, and "" as third json value.
  Otherwise, as an answer, wrote "false" as second json value and
  write a comment with an explanation and necessarily
  indicate the exact incorrect fragment as a quote, enclosed in quotation marks "" as third json value.
chat-gpt-model: gpt-4
chat-gpt-temperature: 0.5
chat-gpt-max-tokens: 2000
```

### default-language

Default language for root files. For example ```default-language: en``` - means that the default language of the files is 'en'. ```file.md``` is in ```en``` language, and individual language files are ```file.pl.md```, ```file.de.md``` etc.

### languages

All allowed languages within the given folder. For example:

```yaml
languages:
  - pl
  - en
```

This means that only 'en' and 'pl' language files are allowed within this folder and each file should be available in these language versions.

### ignore-files

List of files (names only, no path, including *.md extension) that will be ignored during checking. For example:

```yaml
ignore-files:
  - undone.md
  - undone.pl.md
```

Means these two files in the folder will not be considered for checking.

### required-headers

A list of mandatory headers that must be present in each file. For example:

```yaml
required-headers:
  - slug
  - title
  - description
  - author
  - date
```

Means that each file in the folder should have the following values defined in the header (example markdown file):

```yaml
---
slug: article-1
title: Article number 1
description: Something about something
author: John
date: 2023-12-01
---

Article about something is here
```

### required-lists

List of required header values for each language version. For example:

```yaml
required-lists:
  series:
    en:
      - Personal experience
      - Existential question
      - Practice
    pl:
      - Osobiste doświadczenie
      - Egzystencjalne pytanie
      - Praktyka
  categories:
    en:
      - For everyone
      - For beginners
      - For advanced
    pl:
      - Dla wszystkich
      - Dla początkujących
      - Dla zaawansowanych
```

Means that if there is a section with a name specified in the configuration in the file, it must contain a value from the list above. Example mark down file:

```yaml
---
series: 
  - Personal experience
categories: 
  - Other
---

Any text.
```

In the example above, an error will occur because the 'en' language file does not contain the 'Other' category. A required value from the list: 'For everyone', 'For beginners' or 'For advanced'.

If a given section is not required (see `required-headers`) and is not written in the header, then such a check will not take place.

### check-header-duplicates

Sections listed here should contain values that are unique within the entire folder. This is especially useful for values like `title` or `slug`. For example:

```yaml
check-header-duplicates:
  - slug
  - title
```

Example markdown file:

```yaml
---
slug: slug-1
title: Any Title
---

Article
```

If one file has a slug named `slug-1` then if another file with the same name is detected, an error message will appear.

### check-language-structure

If this option is checked, system will check if all language files are properly configured according to `default-language` and `languages`. The folder should contain files in all language versions. For example:

```yaml
default-language: en
languages:
  - en
  - pl
```

Means that all files of the form should be contained in the current folder.

```
content\about.md
content\about.pl.md
content\contact.md
content\contact.pl.md
```

### check-file-language

if the option is selected, the system will check whether the content of the file matches the declared language.

### check-mark-down

If the option is selected, the system will check the correctness of the markdown file.

### check-slug-regex

if the option is checked, the system will validate the slug based on regex.

```yaml
check-slug-regex: yes
pattern-slug-regex: ^[a-z0-9]+(?:-[a-z0-9]+)*$
```

Example mark down file:

```
---
slug: alibaba-12
title: Alibaba 
---

Magic article.
```

In this example, slug is compatible with the regex.

### check-spelling

If the option is enabled, check the article for correctness. Example:

```yaml
check-spelling: yes
```

There are some built in dictionaries for the following languages:

| Language     | Two letter | Culture | Dictionary | Affix file |
|--------------|------------|---------|------------|------------|
| Deutsch      | de         | de-DE   | de_DE.dic  | de_DE.aff  |
| English (US) | en         | en-US   | en_US.dic  | en_US.aff  |
| Español      | es         | es-ES   | es_ES.dic  | es_ES.aff  |
| Français     | fr         | fr-FR   | fr_FR.dic  | fr_FR.aff  |
| Italiano     | it         | it-IT   | it_IT.dic  | it_IT.aff  |
| Polish       | pl         | pl-PL   | pl_PL.dic  | pl_PL.aff  |
| Português    | pt         | pt-PT   | pt_PT.dic  | pt_PT.aff  |

If you need more languages, you can add your dictionaries into main hugo directory. 

Spell checking works with dictionaries and affix files coming from Open Office [Hunspell format](https://hunspell.github.io/).
If you need more languages, you can download them from [this GitHub repository](https://github.com/titoBouzout/Dictionaries)
and put them into destination directory to work with. These files are in UTF-8 format.

Another way is to go to the website [softmaker.com](https://www.softmaker.com/en/download/dictionaries)
and download dictionary you wish. A total of 85 language dictionaries are available.
After downloading, change the file name extension `.sox` to `.zip` and open the file. You should see some files inside.
Unpack `*.dic` and `*.aff` files to the destination directory, an that's all.
You can use these files by `HugoChecker`.

### chat-gpt-spell-check

If the option is enabled, it means that chatgpt will be used to deeply check the article for correctness. Example:

```yaml
chat-gpt-spell-check: no
chat-gpt-prompt: >
  Your role is to check the text message provided by the user in the next messages.
  You will have to tasks to done. And result of the task put in an answer as json, 
  see example below:
  {
      "Language": "en",
      "SpellCheck": true
      "Comment": "Everything is ok"
  }
  
  Task 1: Language detection.
  
  Your role is to identify language of the text message provided by the user in the next messages.
  Do not ask any questions - just make a judgement.
  If you are not sure about the language, choose the most probable one.
  Your answer should be only two letter code of the language (ISO 639-1 code), 
  as the first json value (in our example it is "en").

  Task 2: Spellcheck.

  Your role is to check the correctness of the text in terms of style and grammar.
  Do not ask any questions - just make a judgement.
  As an answer in one word "true" - if everything is correct, as second json value, and "" as third json value.
  Otherwise, as an answer, wrote "false" as second json value and
  write a comment with an explanation and necessarily
  indicate the exact incorrect fragment as a quote, enclosed in quotation marks "" as third json value.
chat-gpt-model: gpt-4
chat-gpt-temperature: 0.5
chat-gpt-max-tokens: 2000
```

You can freely change the prompt for better results according to your specific requirements. It is only important that chatGPT should returns a value in the form of json.

```json
  {
      "Language": "en",
      "SpellCheck": true
      "Comment": "Everything is ok"
  }
```

whereas:
* `language` means detected language two letter code
* `SpellCheck` should be `true` if everything fine, `false` otherwise.
* `Comment` if the grammar correction indicated a mistake.


### chat-gpt-model

Model used by chat-gpt to build responses. For example: gpt-4, gpt-3.5-turbo, gpt-4-32k. More details [here](https://platform.openai.com/docs/guides/gpt).

## Command Line

You can execute action from command line. 

```bash
HugoChecker hugo-folder [chatgpt-api-key]
```

Whereas:

1. The input `hugo-folder` is the root folder of the Hugo project.
2. The input `chatgpt-api-key` is used for spell check and grammar. It's unecessary if you don't need ChatGPT spell check.

Three versions of the command line program are available in the `dist` folder.

1. MacOS: osx-x64/HugoChecker
2. linux-x64/HugoChecker
3. win-x64/HugoChecker.exe
