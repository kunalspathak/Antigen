from bs4 import BeautifulSoup
import re

# Load the HTML file
with open("abs.html", "r", encoding="utf-8") as file:
    html_content = file.read()
    
# Mapping for word replacements (case-sensitive)
replacement_mapping = {
    'Rm': 'm',
    'Rn': 'n',
    'Rd': 'd',
    'Rt': 't',
    'imm|immediate': 'i'
}

# Create a regex pattern for word replacements
pattern = re.compile('|'.join(r'\b{}\b'.format(re.escape(word)) for word in replacement_mapping.keys()))

# Parse the HTML
soup = BeautifulSoup(html_content, 'html.parser')

# Find the table with class "regdiagram"
table = soup.find('table', class_='regdiagram')

# Find all rows in the table
rows = table.find_all('tr')

# Extract data from the second row with word replacements
if len(rows) > 1:
    second_row = rows[1]  # Index 1 corresponds to the second row
    cells = second_row.find_all('td')
    
    # Extract the data from each cell with word replacements
    extracted_data = []
    non_numeric_output = []
    for cell in cells:
        # Check if the cell spans multiple columns
        if 'colspan' in cell.attrs:
            colspan = int(cell['colspan'])
            cell_text = cell.get_text().strip()
            # Replace words using the regex pattern for the entire cell content
            cell_text = pattern.sub(lambda x: replacement_mapping.get(x.group(0), x.group(0)), cell_text)
            extracted_data.extend([cell_text] * colspan)
            # Replace non-numeric output with 0 for the second output
            non_numeric_output.extend(['0'] * colspan)
        else:
            cell_text = cell.get_text().strip()
            # Replace words using the regex pattern
            cell_text = pattern.sub(lambda x: replacement_mapping.get(x.group(0), x.group(0)), cell_text)
            extracted_data.append(cell_text)
            # Replace non-numeric output with 0 for the second output
            non_numeric_output.append('0')

    # Print the extracted data with word replacements
    print("Extracted Data:")
    print(extracted_data)

    # Print the second output with non-numeric values replaced by 0
    print("\nSecond Output (Non-numeric values replaced by 0):")
    print(non_numeric_output)
else:
    print("Table does not have enough rows.")
    
# Extracted Data:
# ['sf', '1', '0', '1', '1', '0', '1', '0', '1', '1', '0', '0', '0', '0', '0', '0', '0', '0', '1', '0', '0', '0', 'n', 'n', 'n', 'n', 'n', 'd', 'd', 'd', 'd', 'd']

# Second Output (Non-numeric values replaced by 0):
# ['0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0']