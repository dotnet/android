<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xalan="http://xml.apache.org/xalan">

  <xsl:output method="xml"
              encoding="UTF-8"
              indent="yes"
              xalan:indent-amount="2"
              standalone="no"
              cdata-section-elements="javadoc"/>

  <xsl:strip-space elements="*"/>

  <xsl:template match="node()|@*">
    <xsl:copy>
    <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>


  <!-- Remove extra carriage returns from `<javadoc/>` CDATA elements to help standardize output across Unix and Windows -->
  <xsl:template match="javadoc/text()">
    <xsl:call-template name="removeCarriageReturn"/>
  </xsl:template>

  <xsl:template name="removeCarriageReturn">
    <xsl:param name="pText" select="."/>

    <xsl:if test="string-length($pText) >0">
      <xsl:choose>
        <xsl:when test="not(contains($pText,'&#13;'))">
          <xsl:value-of select="$pText"/>
        </xsl:when>

        <xsl:otherwise>
          <xsl:value-of select="substring-before($pText, '&#13;')"/>
          <xsl:call-template name="removeCarriageReturn">
            <xsl:with-param name="pText" select="substring-after($pText, '&#13;')"/>
          </xsl:call-template>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:if>
  </xsl:template>

</xsl:stylesheet>
